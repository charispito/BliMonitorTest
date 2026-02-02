using BliMonitorTest.util.StoragePathUtil;
using log4net;
using Microsoft.Data.Sqlite;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BliMonitorTest
{
    public partial class ErrorDataQueryWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ErrorDataQueryWindow));

        private int _page = 1;
        private int _pageSize = 50;
        private int _totalCount = 0;

        private bool _isInitializing = true;
        private bool _isBusy = false;

        private const long ExcelLargeThreshold = 10000;
        private volatile bool _excelBuilding = false;
        private string _excelTempPath = null;
        private Task _excelBuildTask = null;
        private CancellationTokenSource _excelCts = null;

        public ErrorDataQueryWindow()
        {
            _isInitializing = true;
            InitializeComponent();
            _isInitializing = false;

            dpFrom.SelectedDate = DateTime.Today.AddDays(-1);
            dpTo.SelectedDate = DateTime.Today;

            // 시간 콤보 초기화
            FillHourCombo(cbFromHour);
            FillMinuteCombo5(cbFromMinute);
            FillHourCombo(cbToHour);
            FillMinuteCombo5(cbToMinute);

            // 기본 범위: 00:00 ~ 23:55
            cbFromHour.SelectedItem = "00";
            cbFromMinute.SelectedItem = "00";
            cbToHour.SelectedItem = "23";
            cbToMinute.SelectedItem = "55";

            Loaded += async (s, e) => await RefreshGridAsync();
        }

        // 외부에서 채널/소스 기본값을 설정하고 싶을 때 호출
        public void SetInitialFilter(int? sourceType, int? channelNo, string fileNameLike = null)
        {
            if (sourceType.HasValue)
            {
                foreach (var it in cbSourceType.Items)
                {
                    if (it is ComboBoxItem cbi && cbi.Tag?.ToString() == sourceType.Value.ToString())
                    { cbi.IsSelected = true; break; }
                }
            }
            if (channelNo.HasValue) tbChannelNo.Text = channelNo.Value.ToString();
            if (!string.IsNullOrWhiteSpace(fileNameLike)) tbFileNameLike.Text = fileNameLike;
        }

        private sealed class QueryResult
        {
            public int TotalCount;
            public int AppliedPage;
            public int LastPage;
            public DataTable Table;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            if (BusyOverlay != null)
                BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            if (_isBusy) return;

            if (!_excelBuilding && !string.IsNullOrEmpty(_excelTempPath))
                ResetExcelState(deleteTempFile: true);

            _page = 1;
            await RefreshGridAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            if (!IsLoaded) return;
            if (_isBusy) return;

            _page = 1;
            await RefreshGridAsync();
        }

        private async void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (!IsLoaded) return;
            if (_isBusy) return;

            if (cbPageSize.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int size))
            {
                _pageSize = size;
                _page = 1;
                await RefreshGridAsync();
            }
        }

        private async void First_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; _page = 1; await RefreshGridAsync(); }
        private async void Prev_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; if (_page > 1) _page--; await RefreshGridAsync(); }
        private async void Next_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; int last = GetLastPage(); if (_page < last) _page++; await RefreshGridAsync(); }
        private async void Last_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; _page = GetLastPage(); await RefreshGridAsync(); }

        private int GetLastPage()
        {
            if (_pageSize <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));
        }

        private async Task RefreshGridAsync()
        {
            if (!IsLoaded) return;
            if (_isBusy) return;
            if (grid == null || txtPageInfo == null) return;

            DateTime baseFrom = (dpFrom.SelectedDate ?? DateTime.Today).Date;
            DateTime baseTo = (dpTo.SelectedDate ?? DateTime.Today).Date;

            if (!TryGetTimeFromCombos(cbFromHour, cbFromMinute, out var fromTs))
            {
                ToastMessage.ToastService.AppToast.Show("시작 시간을 선택하세요.");
                return;
            }
            if (!TryGetTimeFromCombos(cbToHour, cbToMinute, out var toTs))
            {
                ToastMessage.ToastService.AppToast.Show("종료 시간을 선택하세요.");
                return;
            }

            DateTime fromDateTime = baseFrom.Add(fromTs);
            DateTime toDateTime = baseTo.Add(toTs);

            // 종료 시각 포함 → [from, toExclusive)
            DateTime toExclusive = toDateTime.AddMinutes(5);

            if (toExclusive <= fromDateTime)
            {
                ToastMessage.ToastService.AppToast.Show("기간이 올바르지 않습니다.");
                return;
            }
            if ((toExclusive - fromDateTime).TotalDays > 31)
            {
                ToastMessage.ToastService.AppToast.Show("기간 조회는 최대 1달(31일)까지만 가능합니다.");
                return;
            }

            long fromMs = new DateTimeOffset(fromDateTime).ToUnixTimeMilliseconds();
            long toMs = new DateTimeOffset(toExclusive).ToUnixTimeMilliseconds();

            int sourceType = 0; // 0=전체, 1=단일, 2=다채널
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);
            int? channelNo = TryParseNullableInt(tbChannelNo.Text);

            string fileNameLike = (tbFileNameLike.Text ?? "").Trim();
            int? errorSlot = TryParseNullableInt(tbErrorSlot.Text);
            string errorTextLike = (tbErrorTextLike.Text ?? "").Trim();

            int? mode = TryParseNullableInt(tbMode.Text);

            double? heaterMin = TryParseNullableDouble(tbHeaterMin.Text);
            double? heaterMax = TryParseNullableDouble(tbHeaterMax.Text);
            double? exhaustMin = TryParseNullableDouble(tbExhaustMin.Text);
            double? exhaustMax = TryParseNullableDouble(tbExhaustMax.Text);
            double? hotAirMin = TryParseNullableDouble(tbHotAirMin.Text);
            double? hotAirMax = TryParseNullableDouble(tbHotAirMax.Text);
            double? offMin = TryParseNullableDouble(tbOffMin.Text);
            double? offMax = TryParseNullableDouble(tbOffMax.Text);
            double? onMin = TryParseNullableDouble(tbOnMin.Text);
            double? onMax = TryParseNullableDouble(tbOnMax.Text);
            int? runMin = TryParseNullableInt(tbRunMin.Text);

            string dbPath = StoragePathUtil.GetDbPath();
            if (!File.Exists(dbPath))
            {
                ToastMessage.ToastService.AppToast.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                return;
            }

            int requestedPage = _page;
            int pageSize = _pageSize;

            SetBusy(true);

            try
            {
                QueryResult result = await Task.Run(() =>
                {
                    string cs = $"Data Source={dbPath};";
                    using (var con = new SqliteConnection(cs))
                    {
                        con.Open();

                        string where = BuildWhere(
                            sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                            mode, heaterMin, heaterMax, exhaustMin, exhaustMax,
                            hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin
                        );

                        int totalCount;
                        using (var cmdCount = con.CreateCommand())
                        {
                            cmdCount.CommandText = "SELECT COUNT(1) FROM error_events " + where + ";";
                            BindParams(cmdCount, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                       mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);
                            totalCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        }

                        int lastPage = (pageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                        int appliedPage = Math.Min(Math.Max(1, requestedPage), lastPage);
                        int offset = (appliedPage - 1) * pageSize;

                        var dt = new DataTable();
                        using (var cmd = con.CreateCommand())
                        {
                            cmd.CommandText =
                                "SELECT created_at, source_type, channel_no, file_name, snapshot_id, error_slot, error_text, " +
                                "       run_mode, heater_temp, heater_off_time, hot_air_temp, hot_air_on_time, run_count, exhaust_temp " +
                                "FROM error_events " +
                                where +
                                " ORDER BY created_at_ms DESC " +
                                " LIMIT @limit OFFSET @offset;";

                            BindParams(cmd, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                       mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);
                            cmd.Parameters.AddWithValue("@limit", pageSize);
                            cmd.Parameters.AddWithValue("@offset", offset);

                            using (var r = cmd.ExecuteReader())
                                dt.Load(r);
                        }

                        return new QueryResult
                        {
                            TotalCount = totalCount,
                            AppliedPage = appliedPage,
                            LastPage = lastPage,
                            Table = dt
                        };
                    }
                });

                _totalCount = result.TotalCount;
                _page = result.AppliedPage;

                grid.ItemsSource = result.Table.DefaultView;
                txtPageInfo.Text = $"총 {_totalCount:N0}건        {_page:N0} / {result.LastPage:N0} 페이지";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "조회 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static string BuildWhere(
            int sourceType, int? channelNo, string fileNameLike, int? errorSlot, string errorTextLike,
            int? mode, double? heaterMin, double? heaterMax, double? exhaustMin, double? exhaustMax,
            double? hotAirMin, double? hotAirMax, double? offMin, double? offMax, double? onMin, double? onMax,
            int? runMin
        )
        {
            string where = "WHERE created_at_ms >= @fromMs AND created_at_ms < @toMs";
            where += " AND (@sourceType = 0 OR source_type = @sourceType)";
            where += " AND (@channelNo = 0 OR channel_no = @channelNo)";

            if (!string.IsNullOrWhiteSpace(fileNameLike)) where += " AND file_name LIKE @fileNameLike";
            if (errorSlot.HasValue) where += " AND error_slot = @errorSlot";
            if (!string.IsNullOrWhiteSpace(errorTextLike)) where += " AND error_text LIKE @errorTextLike";

            if (mode.HasValue) where += " AND run_mode = @mode";
            if (heaterMin.HasValue) where += " AND heater_temp >= @heaterMin";
            if (heaterMax.HasValue) where += " AND heater_temp <= @heaterMax";
            if (exhaustMin.HasValue) where += " AND exhaust_temp >= @exhaustMin";
            if (exhaustMax.HasValue) where += " AND exhaust_temp <= @exhaustMax";
            if (hotAirMin.HasValue) where += " AND hot_air_temp >= @hotAirMin";
            if (hotAirMax.HasValue) where += " AND hot_air_temp <= @hotAirMax";
            if (offMin.HasValue) where += " AND heater_off_time >= @offMin";
            if (offMax.HasValue) where += " AND heater_off_time <= @offMax";
            if (onMin.HasValue) where += " AND hot_air_on_time >= @onMin";
            if (onMax.HasValue) where += " AND hot_air_on_time <= @onMax";
            if (runMin.HasValue) where += " AND run_count >= @runMin";

            return where;
        }

        private static void BindParams(
            SqliteCommand cmd, long fromMs, long toMs, int sourceType, int? channelNo, string fileNameLike, int? errorSlot, string errorTextLike,
            int? mode, double? heaterMin, double? heaterMax, double? exhaustMin, double? exhaustMax,
            double? hotAirMin, double? hotAirMax, double? offMin, double? offMax, double? onMin, double? onMax,
            int? runMin
        )
        {
            cmd.Parameters.AddWithValue("@fromMs", fromMs);
            cmd.Parameters.AddWithValue("@toMs", toMs);

            cmd.Parameters.AddWithValue("@sourceType", sourceType);
            cmd.Parameters.AddWithValue("@channelNo", channelNo.HasValue ? channelNo.Value : 0);

            if (!string.IsNullOrWhiteSpace(fileNameLike)) cmd.Parameters.AddWithValue("@fileNameLike", $"%{fileNameLike}%");
            if (errorSlot.HasValue) cmd.Parameters.AddWithValue("@errorSlot", errorSlot.Value);
            if (!string.IsNullOrWhiteSpace(errorTextLike)) cmd.Parameters.AddWithValue("@errorTextLike", $"%{errorTextLike}%");

            if (mode.HasValue) cmd.Parameters.AddWithValue("@mode", mode.Value);

            if (heaterMin.HasValue) cmd.Parameters.AddWithValue("@heaterMin", heaterMin.Value);
            if (heaterMax.HasValue) cmd.Parameters.AddWithValue("@heaterMax", heaterMax.Value);
            if (exhaustMin.HasValue) cmd.Parameters.AddWithValue("@exhaustMin", exhaustMin.Value);
            if (exhaustMax.HasValue) cmd.Parameters.AddWithValue("@exhaustMax", exhaustMax.Value);
            if (hotAirMin.HasValue) cmd.Parameters.AddWithValue("@hotAirMin", hotAirMin.Value);
            if (hotAirMax.HasValue) cmd.Parameters.AddWithValue("@hotAirMax", hotAirMax.Value);
            if (offMin.HasValue) cmd.Parameters.AddWithValue("@offMin", offMin.Value);
            if (offMax.HasValue) cmd.Parameters.AddWithValue("@offMax", offMax.Value);
            if (onMin.HasValue) cmd.Parameters.AddWithValue("@onMin", onMin.Value);
            if (onMax.HasValue) cmd.Parameters.AddWithValue("@onMax", onMax.Value);
            if (runMin.HasValue) cmd.Parameters.AddWithValue("@runMin", runMin.Value);
        }

        private int? TryParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s.Trim(), out int v) ? v : (int?)null;
        }

        private double? TryParseNullableDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s.Trim(), out double v) ? v : (double?)null;
        }

        // 엑셀 출력
        private async void ExcelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            if (_excelBuilding) return;

            DateTime baseFrom = (dpFrom.SelectedDate ?? DateTime.Today).Date;
            DateTime baseTo = (dpTo.SelectedDate ?? DateTime.Today).Date;

            if (!TryGetTimeFromCombos(cbFromHour, cbFromMinute, out var fromTs))
            {
                ToastMessage.ToastService.AppToast.Show("시작 시간을 선택하세요.");
                return;
            }
            if (!TryGetTimeFromCombos(cbToHour, cbToMinute, out var toTs))
            {
                ToastMessage.ToastService.AppToast.Show("종료 시간을 선택하세요.");
                return;
            }

            DateTime fromDateTime = baseFrom.Add(fromTs);
            DateTime toDateTime = baseTo.Add(toTs);
            DateTime toExclusive = toDateTime.AddMinutes(5);

            if (toExclusive <= fromDateTime)
            {
                ToastMessage.ToastService.AppToast.Show("기간이 올바르지 않습니다.");
                return;
            }
            if ((toExclusive - fromDateTime).TotalDays > 31)
            {
                ToastMessage.ToastService.AppToast.Show("기간 조회는 최대 1달(31일)까지만 가능합니다.");
                return;
            }

            long fromMs = new DateTimeOffset(fromDateTime).ToUnixTimeMilliseconds();
            long toMs = new DateTimeOffset(toExclusive).ToUnixTimeMilliseconds();

            int sourceType = 0;
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);
            int? channelNo = TryParseNullableInt(tbChannelNo.Text);

            string fileNameLike = (tbFileNameLike.Text ?? "").Trim();
            int? errorSlot = TryParseNullableInt(tbErrorSlot.Text);
            string errorTextLike = (tbErrorTextLike.Text ?? "").Trim();

            int? mode = TryParseNullableInt(tbMode.Text);
            double? heaterMin = TryParseNullableDouble(tbHeaterMin.Text);
            double? heaterMax = TryParseNullableDouble(tbHeaterMax.Text);
            double? exhaustMin = TryParseNullableDouble(tbExhaustMin.Text);
            double? exhaustMax = TryParseNullableDouble(tbExhaustMax.Text);
            double? hotAirMin = TryParseNullableDouble(tbHotAirMin.Text);
            double? hotAirMax = TryParseNullableDouble(tbHotAirMax.Text);
            double? offMin = TryParseNullableDouble(tbOffMin.Text);
            double? offMax = TryParseNullableDouble(tbOffMax.Text);
            double? onMin = TryParseNullableDouble(tbOnMin.Text);
            double? onMax = TryParseNullableDouble(tbOnMax.Text);
            int? runMin = TryParseNullableInt(tbRunMin.Text);

            string dbPath = StoragePathUtil.GetDbPath();
            if (!File.Exists(dbPath))
            {
                ToastMessage.ToastService.AppToast.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                return;
            }

            // 건수 파악
            long totalCount = await Task.Run(() =>
            {
                string cs = $"Data Source={dbPath};";
                using (var con = new SqliteConnection(cs))
                {
                    con.Open();
                    string where = BuildWhere(sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                              mode, heaterMin, heaterMax, exhaustMin, exhaustMax,
                                              hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM error_events " + where + ";";
                        BindParams(cmd, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                   mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);
                        return Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }
            });

            if (totalCount <= 0)
            {
                ToastMessage.ToastService.AppToast.Show("조건에 맞는 데이터가 없습니다.");
                return;
            }

            if (totalCount >= ExcelLargeThreshold)
            {
                var res = MessageBox.Show(
                    $"조건에 맞는 데이터가 {totalCount:N0}건입니다.\n대용량은 엑셀 생성에 시간이 걸릴 수 있어 백그라운드에서 생성합니다.\n\n지금 생성할까요?",
                    "대용량 엑셀 생성",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                if (res != MessageBoxResult.Yes) return;

                StartLargeExcelBuildInBackground(dbPath, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                                 mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin, totalCount);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"error_events_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            SetBusy(true);
            try
            {
                await Task.Run(() =>
                {
                    ExportAllToExcelOpenXml(dbPath, sfd.FileName, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                            mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin,
                                            progress: null, token: CancellationToken.None);
                });
                ToastMessage.ToastService.AppToast.Show("엑셀 다운로드가 완료되었습니다.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void StartLargeExcelBuildInBackground(
            string dbPath, long fromMs, long toMs, int sourceType, int? channelNo, string fileNameLike, int? errorSlot, string errorTextLike,
            int? mode, double? heaterMin, double? heaterMax, double? exhaustMin, double? exhaustMax,
            double? hotAirMin, double? hotAirMax, double? offMin, double? offMax, double? onMin, double? onMax, int? runMin, long totalCount
        )
        {
            _excelBuilding = true;
            _excelCts = new CancellationTokenSource();

            if (btnExcel != null)
            {
                btnExcel.IsEnabled = false;
                btnExcel.Content = "생성 중...";
                btnExcel.ToolTip = null;
            }

            if (btnCancelBusy != null)
                btnCancelBusy.Visibility = Visibility.Visible;

            string tempDir = Path.Combine(Path.GetTempPath(), "BliMonitorTest");
            Directory.CreateDirectory(tempDir);

            string tmpPath = Path.Combine(tempDir, $"error_events_{DateTime.Now:yyyyMMdd_HHmmss}.tmp.xlsx");
            string finalTempPath = Path.Combine(tempDir, $"error_events_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            _excelTempPath = null;

            var progress = new Progress<(int percent, long done, long total)>(p =>
            {
                if (btnExcel != null)
                    btnExcel.ToolTip = $"{p.percent}% ({p.done:N0}/{p.total:N0})";
            });

            var token = _excelCts.Token;

            _excelBuildTask = Task.Run(() =>
            {
                try
                {
                    ExportAllToExcelOpenXml(dbPath, tmpPath, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                            mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin,
                                            progress, token);

                    token.ThrowIfCancellationRequested();

                    if (File.Exists(finalTempPath)) File.Delete(finalTempPath);
                    File.Move(tmpPath, finalTempPath);

                    return finalTempPath;
                }
                catch
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                    throw;
                }
            }, token)
            .ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    _excelBuilding = false;

                    if (btnCancelBusy != null)
                        btnCancelBusy.Visibility = Visibility.Collapsed;

                    _excelCts?.Dispose();
                    _excelCts = null;

                    if (t.IsCanceled || (t.IsFaulted && t.Exception?.GetBaseException() is OperationCanceledException))
                    {
                        if (btnExcel != null)
                        {
                            btnExcel.IsEnabled = true;
                            btnExcel.Content = "엑셀";
                            btnExcel.ToolTip = null;
                        }
                        ToastMessage.ToastService.AppToast.Show("엑셀 생성이 취소되었습니다.");
                        return;
                    }

                    if (t.IsFaulted)
                    {
                        if (btnExcel != null)
                        {
                            btnExcel.IsEnabled = true;
                            btnExcel.Content = "엑셀";
                            btnExcel.ToolTip = null;
                        }
                        MessageBox.Show(t.Exception?.GetBaseException().ToString() ?? "엑셀 생성 오류", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    _excelTempPath = t.Result;

                    if (btnExcel != null)
                    {
                        btnExcel.IsEnabled = true;
                        btnExcel.Content = "엑셀 저장";
                        btnExcel.ToolTip = "엑셀 생성 완료";
                    }

                    MessageBox.Show(
                        $"대용량 엑셀 생성이 완료되었습니다.\n\n'엑셀 저장'을 누르면 원하는 위치에 저장할 수 있습니다.\n(건수: {totalCount:N0})",
                        "완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                });
            });
        }

        private static void ExportAllToExcelOpenXml(
            string dbPath, string xlsxPath, long fromMs, long toMs, int sourceType, int? channelNo, string fileNameLike, int? errorSlot, string errorTextLike,
            int? mode, double? heaterMin, double? heaterMax, double? exhaustMin, double? exhaustMax,
            double? hotAirMin, double? hotAirMax, double? offMin, double? offMax, double? onMin, double? onMax, int? runMin,
            IProgress<(int percent, long done, long total)> progress, CancellationToken token
        )
        {
            string where = BuildWhere(sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                                      mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);

            using (var con = new SqliteConnection($"Data Source={dbPath};"))
            {
                con.Open();

                long total;
                using (var cmdCount = con.CreateCommand())
                {
                    cmdCount.CommandText = "SELECT COUNT(1) FROM error_events " + where + ";";
                    BindParams(cmdCount, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                               mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);
                    total = Convert.ToInt64(cmdCount.ExecuteScalar());
                }

                progress?.Report((0, 0, total));

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT created_at, source_type, channel_no, file_name, snapshot_id, error_slot, error_text, " +
                        "       run_mode, heater_temp, heater_off_time, hot_air_temp, hot_air_on_time, run_count, exhaust_temp " +
                        "FROM error_events " +
                        where +
                        " ORDER BY created_at_ms DESC;";

                    BindParams(cmd, fromMs, toMs, sourceType, channelNo, fileNameLike, errorSlot, errorTextLike,
                               mode, heaterMin, heaterMax, exhaustMin, exhaustMax, hotAirMin, hotAirMax, offMin, offMax, onMin, onMax, runMin);

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                    using (var doc = SpreadsheetDocument.Create(xlsxPath, SpreadsheetDocumentType.Workbook))
                    {
                        var wbPart = doc.AddWorkbookPart();
                        wbPart.Workbook = new Workbook();
                        var sheets = wbPart.Workbook.AppendChild(new Sheets());

                        uint sheetId = 1;
                        long written = 0;

                        const int MaxRowsPerSheet = 1048576;

                        WorksheetPart wsPart = null;
                        OpenXmlWriter writer = null;
                        int currentRow = 0;
                        int sheetIndex = 1;

                        void StartNewSheet()
                        {
                            if (writer != null)
                            {
                                writer.WriteEndElement(); // SheetData
                                writer.WriteEndElement(); // Worksheet
                                writer.Close();
                                writer = null;
                            }
                            wsPart = wbPart.AddNewPart<WorksheetPart>();
                            writer = OpenXmlWriter.Create(wsPart);
                            writer.WriteStartElement(new Worksheet());
                            writer.WriteStartElement(new SheetData());

                            var sheet = new Sheet
                            {
                                Id = wbPart.GetIdOfPart(wsPart),
                                SheetId = sheetId++,
                                Name = $"error_events_{sheetIndex++}"
                            };
                            sheets.Append(sheet);

                            currentRow = 0;
                            WriteHeaderRow(writer, reader);
                            currentRow++;
                        }

                        StartNewSheet();

                        while (reader.Read())
                        {
                            if (currentRow >= MaxRowsPerSheet)
                                StartNewSheet();

                            WriteDataRow(writer, reader);
                            currentRow++;
                            written++;

                            if (total > 0 && (written % 500) == 0)
                            {
                                int percent = (int)(written * 100 / total);
                                progress?.Report((percent, written, total));
                            }
                            token.ThrowIfCancellationRequested();
                        }

                        if (writer != null)
                        {
                            writer.WriteEndElement(); // SheetData
                            writer.WriteEndElement(); // Worksheet
                            writer.Close();
                        }

                        wbPart.Workbook.Save();
                        progress?.Report((100, written, total));
                    }
                }
            }
        }

        private static void WriteHeaderRow(OpenXmlWriter writer, SqliteDataReader reader)
        {
            writer.WriteStartElement(new Row());
            for (int i = 0; i < reader.FieldCount; i++)
                WriteTextCell(writer, reader.GetName(i));
            writer.WriteEndElement(); // Row
        }

        private static void WriteDataRow(OpenXmlWriter writer, SqliteDataReader reader)
        {
            writer.WriteStartElement(new Row());
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object v = reader.GetValue(i);
                string s = (v == null || v == DBNull.Value) ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
                WriteTextCell(writer, s);
            }
            writer.WriteEndElement(); // Row
        }

        private static void WriteTextCell(OpenXmlWriter writer, string text)
        {
            writer.WriteElement(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(text ?? ""))
            });
        }

        private void CancelBusy_Click(object sender, RoutedEventArgs e)
        {
            try { _excelCts?.Cancel(); } catch { }
        }

        private void ResetExcelState(bool deleteTempFile)
        {
            try { _excelCts?.Cancel(); } catch { }
            if (deleteTempFile && !string.IsNullOrEmpty(_excelTempPath))
            {
                try { if (File.Exists(_excelTempPath)) File.Delete(_excelTempPath); } catch { }
            }
            _excelTempPath = null;

            if (btnExcel != null)
            {
                btnExcel.IsEnabled = true;
                btnExcel.Content = "엑셀";
                btnExcel.ToolTip = null;
            }
        }

        private static void FillHourCombo(ComboBox cb)
        {
            cb.Items.Clear();
            for (int h = 0; h <= 23; h++)
                cb.Items.Add(h.ToString("00"));
            cb.SelectedIndex = 0;
        }

        private static void FillMinuteCombo5(ComboBox cb)
        {
            cb.Items.Clear();
            for (int m = 0; m < 60; m += 5)
                cb.Items.Add(m.ToString("00"));
            cb.SelectedIndex = 0;
        }

        private static bool TryGetTimeFromCombos(ComboBox cbHour, ComboBox cbMinute, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            if (cbHour?.SelectedItem == null || cbMinute?.SelectedItem == null) return false;
            if (!int.TryParse(cbHour.SelectedItem.ToString(), out int h)) return false;
            if (!int.TryParse(cbMinute.SelectedItem.ToString(), out int m)) return false;
            ts = new TimeSpan(h, m, 0);
            return true;
        }


    }
}
