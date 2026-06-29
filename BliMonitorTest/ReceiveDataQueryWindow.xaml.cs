using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BliMonitorTest.data;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using log4net;
using Microsoft.Data.Sqlite;

namespace BliMonitorTest
{
    public partial class ReceiveDataQueryWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ReceiveDataQueryWindow));

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

        private sealed class ModelNameItem
        {
            public string Value { get; set; }
            public string Display { get; set; }
        }

        private sealed class QueryResult
        {
            public int TotalCount;
            public int AppliedPage;
            public int LastPage;
            public DataTable Table;
        }

        public ReceiveDataQueryWindow()
        {
            _isInitializing = true;
            InitializeComponent();

            InitializeModelNameCombo();

            _isInitializing = false;

            dpFrom.SelectedDate = DateTime.Today.AddDays(-1);
            dpTo.SelectedDate = DateTime.Today;

            FillHourCombo(cbFromHour);
            FillMinuteCombo5(cbFromMinute);
            FillHourCombo(cbToHour);
            FillMinuteCombo5(cbToMinute);

            cbFromHour.SelectedItem = "00";
            cbFromMinute.SelectedItem = "00";
            cbToHour.SelectedItem = "23";
            cbToMinute.SelectedItem = "55";

            Loaded += async (s, e) => await RefreshGridAsync();
        }

        private void InitializeModelNameCombo()
        {
            var items = new List<ModelNameItem>
            {
                new ModelNameItem { Value = null, Display = "전체" },
                new ModelNameItem { Value = "BSH-311", Display = "BSH-311" },
                new ModelNameItem { Value = "BSS-311", Display = "BSS-311" },
                new ModelNameItem { Value = "BSS-314", Display = "BSS-314" },
                new ModelNameItem { Value = "BSS-310", Display = "BSS-310" },
                new ModelNameItem { Value = "BSS-330", Display = "BSS-330" },
                new ModelNameItem { Value = "BSS-341", Display = "BSS-341" },
                new ModelNameItem { Value = "DEWO8", Display = "DEWO8" },
                new ModelNameItem { Value = "HYBRID", Display = "HYBRID" }
            };

            cbModelName.ItemsSource = items;
            cbModelName.SelectedIndex = 0;
        }

        private static int? ResolveModelCodeFromModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return null;

            switch (modelName.Trim().ToUpperInvariant())
            {
                case "BSH-311": return 0;
                case "BSS-311": return 1;
                case "BSS-314": return 2;
                case "BSS-310": return 3;
                case "BSS-330": return 4;
                case "BSS-341": return 5;
                case "DEWO8": return 6;
                case "HYBRID": return 7;
                default: return null;
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            if (BusyOverlay != null)
                BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetBusyMessage(bool busy, string title, string message)
        {
            SetBusy(busy);
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _isBusy) return;

            if (!_excelBuilding && !string.IsNullOrEmpty(_excelTempPath))
                ResetExcelState(true);

            _page = 1;
            await RefreshGridAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            if (!IsLoaded || _isBusy) return;

            _page = 1;
            await RefreshGridAsync();
        }

        private async void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || !IsLoaded || _isBusy) return;

            if (cbPageSize.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int size))
            {
                _pageSize = size;
                _page = 1;
                await RefreshGridAsync();
            }
        }

        private async void First_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            _page = 1;
            await RefreshGridAsync();
        }

        private async void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            if (_page > 1) _page--;
            await RefreshGridAsync();
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            int last = GetLastPage();
            if (_page < last) _page++;
            await RefreshGridAsync();
        }

        private async void Last_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            _page = GetLastPage();
            await RefreshGridAsync();
        }

        private int GetLastPage()
        {
            if (_pageSize <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));
        }

        private async Task RefreshGridAsync()
        {
            if (!IsLoaded || _isBusy || grid == null || txtPageInfo == null)
                return;

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
            DateTime toExclusive = baseTo.Add(toTs).AddMinutes(5);

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

            string selectedModelName = null;
            if (cbModelName?.SelectedItem is ModelNameItem modelItem)
                selectedModelName = modelItem.Value;

            int? modelCode = ResolveModelCodeFromModelName(selectedModelName);

            int sourceType = 0;
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);

            int? channelNo = TryParseNullableInt(tbChannelNo.Text);
            int? hotTempMin = TryParseNullableInt(tbHeaterMin.Text);
            int? hotTempMax = TryParseNullableInt(tbHeaterMax.Text);
            int? coldTempMin = TryParseNullableInt(tbColdMin.Text);
            int? coldTempMax = TryParseNullableInt(tbColdMax.Text);
            int? compressorOutput = GetComboInt(cbCompOn);

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
                    using (var con = new SqliteConnection($"Data Source={dbPath};"))
                    {
                        con.Open();

                        string where = BuildWhere(
                            modelCode,
                            sourceType,
                            channelNo,
                            hotTempMin,
                            hotTempMax,
                            coldTempMin,
                            coldTempMax,
                            compressorOutput
                        );

                        int totalCount;
                        using (var cmdCount = con.CreateCommand())
                        {
                            cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                            BindParams(
                                cmdCount,
                                fromMs,
                                toMs,
                                modelCode,
                                sourceType,
                                channelNo,
                                hotTempMin,
                                hotTempMax,
                                coldTempMin,
                                coldTempMax,
                                compressorOutput
                            );

                            log.Info(MonitoringDb.FormatSqlLog(cmdCount, "RECEIVE_DATA COUNT : "));
                            totalCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        }

                        int lastPage = pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                        int appliedPage = Math.Min(Math.Max(1, requestedPage), lastPage);
                        int offset = (appliedPage - 1) * pageSize;

                        var dt = new DataTable();
                        using (var cmd = con.CreateCommand())
                        {
                            cmd.CommandText =
                                "SELECT " +
                                " id, source_type, channel_no, created_at, created_at_ms, " +
                                " model_code, error_code, " +
                                " water_init_done, water_init_go, empty_detect, buffer_low, " +
                                " reheat_running, hot_ing, heater_pwm, night, test_mode, " +
                                " mode_selected, qty_selected, dispense_phase, dispense_sub_phase, " +
                                " hot_temp_raw, cold_temp_raw, " +
                                " float_low_stable, ball_top_full_stable, water_buf_full_stable, " +
                                " heater_output, compressor_output, hot_valve_output, " +
                                " cold_select_output, outlet_valve_output, " +
                                " button_info, status_a, status_b, " +
                                " command, payload_size, checksum, end_packet " +
                                "FROM receive_data " +
                                where +
                                " ORDER BY created_at_ms DESC " +
                                " LIMIT @limit OFFSET @offset;";

                            BindParams(
                                cmd,
                                fromMs,
                                toMs,
                                modelCode,
                                sourceType,
                                channelNo,
                                hotTempMin,
                                hotTempMax,
                                coldTempMin,
                                coldTempMax,
                                compressorOutput
                            );

                            cmd.Parameters.AddWithValue("@limit", pageSize);
                            cmd.Parameters.AddWithValue("@offset", offset);

                            log.Info(MonitoringDb.FormatSqlLog(cmd, "RECEIVE_DATA LIST"));
                            using (var r = cmd.ExecuteReader())
                            {
                                dt.Load(r);
                            }
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

                ReceiveDataExportFormatter.AddReceiveInterpretColumns(result.Table);
                var displayTable = ReceiveDataExportFormatter.BuildReceiveExportTable(result.Table);

                grid.ItemsSource = displayTable.DefaultView;
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
            int? modelCode,
            int sourceType,
            int? channelNo,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? compressorOutput)
        {
            string where = "WHERE created_at_ms >= @fromMs AND created_at_ms < @toMs";
            where += " AND (@sourceType = 0 OR source_type = @sourceType)";
            where += " AND (@channelNo = 0 OR channel_no = @channelNo)";

            if (modelCode.HasValue)
                where += " AND model_code = @modelCode";

            if (hotTempMin.HasValue)
                where += " AND hot_temp_raw >= @hotTempMin";

            if (hotTempMax.HasValue)
                where += " AND hot_temp_raw <= @hotTempMax";

            if (coldTempMin.HasValue)
                where += " AND cold_temp_raw >= @coldTempMin";

            if (coldTempMax.HasValue)
                where += " AND cold_temp_raw <= @coldTempMax";

            if (compressorOutput.HasValue)
                where += " AND compressor_output = @compressorOutput";

            return where;
        }

        private static void BindParams(
            SqliteCommand cmd,
            long fromMs,
            long toMs,
            int? modelCode,
            int sourceType,
            int? channelNo,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? compressorOutput)
        {
            cmd.Parameters.AddWithValue("@fromMs", fromMs);
            cmd.Parameters.AddWithValue("@toMs", toMs);
            cmd.Parameters.AddWithValue("@sourceType", sourceType);
            cmd.Parameters.AddWithValue("@channelNo", channelNo ?? 0);

            if (modelCode.HasValue)
                cmd.Parameters.AddWithValue("@modelCode", modelCode.Value);

            if (hotTempMin.HasValue)
                cmd.Parameters.AddWithValue("@hotTempMin", hotTempMin.Value);

            if (hotTempMax.HasValue)
                cmd.Parameters.AddWithValue("@hotTempMax", hotTempMax.Value);

            if (coldTempMin.HasValue)
                cmd.Parameters.AddWithValue("@coldTempMin", coldTempMin.Value);

            if (coldTempMax.HasValue)
                cmd.Parameters.AddWithValue("@coldTempMax", coldTempMax.Value);

            if (compressorOutput.HasValue)
                cmd.Parameters.AddWithValue("@compressorOutput", compressorOutput.Value);
        }

        private async void ExcelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            if (_excelBuilding) return;

            if (!string.IsNullOrEmpty(_excelTempPath) && File.Exists(_excelTempPath))
            {
                var sfd2 = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = $"receive_data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (sfd2.ShowDialog() != true)
                    return;

                try
                {
                    File.Copy(_excelTempPath, sfd2.FileName, true);
                    ResetExcelState(true);
                    ToastMessage.ToastService.AppToast.Show("엑셀 파일 저장이 완료되었습니다.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "엑셀 저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

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
            DateTime toExclusive = baseTo.Add(toTs).AddMinutes(5);

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

            string selectedModelName = null;
            if (cbModelName?.SelectedItem is ModelNameItem modelItem)
                selectedModelName = modelItem.Value;

            int? modelCode = ResolveModelCodeFromModelName(selectedModelName);

            int sourceType = 0;
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);

            int? channelNo = TryParseNullableInt(tbChannelNo.Text);
            int? hotTempMin = TryParseNullableInt(tbHeaterMin.Text);
            int? hotTempMax = TryParseNullableInt(tbHeaterMax.Text);
            int? coldTempMin = TryParseNullableInt(tbColdMin.Text);
            int? coldTempMax = TryParseNullableInt(tbColdMax.Text);
            int? compressorOutput = GetComboInt(cbCompOn);

            string dbPath = StoragePathUtil.GetDbPath();
            if (!File.Exists(dbPath))
            {
                ToastMessage.ToastService.AppToast.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                return;
            }

            long totalCount = await Task.Run(() =>
            {
                using (var con = new SqliteConnection($"Data Source={dbPath};"))
                {
                    con.Open();

                    string where = BuildWhere(
                        modelCode,
                        sourceType,
                        channelNo,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        compressorOutput
                    );

                    using (var cmdCount = con.CreateCommand())
                    {
                        cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                        BindParams(
                            cmdCount,
                            fromMs,
                            toMs,
                            modelCode,
                            sourceType,
                            channelNo,
                            hotTempMin,
                            hotTempMax,
                            coldTempMin,
                            coldTempMax,
                            compressorOutput
                        );

                        log.Info(MonitoringDb.FormatSqlLog(cmdCount, "RECEIVE_DATA EXCEL COUNT : "));
                        return Convert.ToInt64(cmdCount.ExecuteScalar());
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
                    MessageBoxImage.Warning);

                if (res != MessageBoxResult.Yes) return;

                StartLargeExcelBuildInBackground(
                    dbPath,
                    fromMs,
                    toMs,
                    modelCode,
                    sourceType,
                    channelNo,
                    hotTempMin,
                    hotTempMax,
                    coldTempMin,
                    coldTempMax,
                    compressorOutput,
                    totalCount
                );

                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"receive_data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (sfd.ShowDialog() != true) return;

            SetBusyMessage(true, "엑셀 생성 중...", "저용량 엑셀 파일을 생성 중입니다.");

            try
            {
                await Task.Run(() =>
                {
                    ExportAllToExcelOpenXml(
                        dbPath,
                        sfd.FileName,
                        fromMs,
                        toMs,
                        modelCode,
                        sourceType,
                        channelNo,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        compressorOutput,
                        null,
                        CancellationToken.None
                    );
                });

                ToastMessage.ToastService.AppToast.Show("엑셀 다운로드가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "엑셀 생성 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void StartLargeExcelBuildInBackground(
            string dbPath,
            long fromMs,
            long toMs,
            int? modelCode,
            int sourceType,
            int? channelNo,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? compressorOutput,
            long totalCount)
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

            string tmpPath = Path.Combine(tempDir, $"receive_data_{DateTime.Now:yyyyMMdd_HHmmss}.tmp.xlsx");
            string finalTempPath = Path.Combine(tempDir, $"receive_data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

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
                    ExportAllToExcelOpenXml(
                        dbPath,
                        tmpPath,
                        fromMs,
                        toMs,
                        modelCode,
                        sourceType,
                        channelNo,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        compressorOutput,
                        progress,
                        token
                    );

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

                        MessageBox.Show(
                            t.Exception?.GetBaseException()?.ToString() ?? "엑셀 생성 오류",
                            "오류",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
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
            string dbPath,
            string xlsxPath,
            long fromMs,
            long toMs,
            int? modelCode,
            int sourceType,
            int? channelNo,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? compressorOutput,
            IProgress<(int percent, long done, long total)> progress,
            CancellationToken token)
        {
            string where = BuildWhere(
                modelCode,
                sourceType,
                channelNo,
                hotTempMin,
                hotTempMax,
                coldTempMin,
                coldTempMax,
                compressorOutput
            );

            using (var con = new SqliteConnection($"Data Source={dbPath};"))
            {
                con.Open();

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT " +
                        " id, source_type, channel_no, created_at, created_at_ms, " +
                        " model_code, error_code, " +
                        " water_init_done, water_init_go, empty_detect, buffer_low, " +
                        " reheat_running, hot_ing, heater_pwm, night, test_mode, " +
                        " mode_selected, qty_selected, dispense_phase, dispense_sub_phase, " +
                        " hot_temp_raw, cold_temp_raw, " +
                        " float_low_stable, ball_top_full_stable, water_buf_full_stable, " +
                        " heater_output, compressor_output, hot_valve_output, " +
                        " cold_select_output, outlet_valve_output, " +
                        " button_info, status_a, status_b, " +
                        " command, payload_size, checksum, end_packet " +
                        "FROM receive_data " +
                        where +
                        " ORDER BY created_at_ms DESC;";

                    BindParams(
                        cmd,
                        fromMs,
                        toMs,
                        modelCode,
                        sourceType,
                        channelNo,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        compressorOutput
                    );

                    log.Info(MonitoringDb.FormatSqlLog(cmd, "RECEIVE_DATA EXPORT DATA : "));

                    var dt = new DataTable();
                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }

                    ReceiveDataExportFormatter.AddReceiveInterpretColumns(dt);
                    var exportTable = ReceiveDataExportFormatter.BuildReceiveExportTable(dt);

                    long totalRows = exportTable.Rows.Count;
                    progress?.Report((0, 0, totalRows));

                    using (var doc = SpreadsheetDocument.Create(xlsxPath, SpreadsheetDocumentType.Workbook))
                    {
                        var wbPart = doc.AddWorkbookPart();
                        wbPart.Workbook = new Workbook();
                        var sheets = wbPart.Workbook.AppendChild(new Sheets());

                        uint sheetId = 1;
                        const int MaxRowsPerSheet = 1048576;

                        WorksheetPart wsPart = null;
                        OpenXmlWriter writer = null;
                        int currentRowInSheet = 0;
                        int sheetIndex = 1;
                        long written = 0;

                        void StartNewSheet()
                        {
                            if (writer != null)
                            {
                                writer.WriteEndElement();
                                writer.WriteEndElement();
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
                                Name = $"receive_data_{sheetIndex++}"
                            };
                            sheets.Append(sheet);

                            currentRowInSheet = 0;
                            //WriteHeaderRow(writer, dt);
                            WriteHeaderRow(writer, exportTable);
                            currentRowInSheet++;
                        }

                        StartNewSheet();

                        foreach (DataRow row in exportTable.Rows)
                        {
                            if (currentRowInSheet >= MaxRowsPerSheet)
                                StartNewSheet();

                            //WriteDataRow(writer, dt, row);
                            WriteDataRow(writer, exportTable, row);
                            currentRowInSheet++;
                            written++;

                            if (totalRows > 0 && (written % 500) == 0)
                            {
                                int percent = (int)(written * 100 / totalRows);
                                progress?.Report((percent, written, totalRows));
                            }

                            token.ThrowIfCancellationRequested();
                        }

                        if (writer != null)
                        {
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                            writer.Close();
                        }

                        wbPart.Workbook.Save();
                        progress?.Report((100, written, totalRows));
                    }
                }
            }
        }

        private static void WriteHeaderRow(OpenXmlWriter writer, DataTable dt)
        {
            writer.WriteStartElement(new Row());
            foreach (DataColumn col in dt.Columns)
                WriteTextCell(writer, col.ColumnName);
            writer.WriteEndElement();
        }

        private static void WriteDataRow(OpenXmlWriter writer, DataTable dt, DataRow row)
        {
            writer.WriteStartElement(new Row());
            foreach (DataColumn col in dt.Columns)
            {
                object v = row[col];
                string s = (v == null || v == DBNull.Value) ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
                WriteTextCell(writer, s);
            }
            writer.WriteEndElement();
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

        private void Grid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            foreach (var col in grid.Columns)
            {
                col.CanUserSort = true;

                if (col.Header != null)
                {
                    string header = col.Header.ToString();

                    if (header == "수신시각")
                        col.Width = 150;
                    else if (header == "Source")
                        col.Width = 90;
                    else if (header == "채널번호")
                        col.Width = 80;
                    else if (header == "모델명")
                        col.Width = 100;
                    else if (header == "에러해석")
                        col.Width = 140;
                    else if (header == "선택모드")
                        col.Width = 100;
                    else if (header == "선택용량")
                        col.Width = 100;
                    else if (header == "출수단계")
                        col.Width = 120;
                    else if (header == "출수세부단계")
                        col.Width = 140;
                    else if (header == "온수Temp")
                        col.Width = 90;
                    else if (header == "냉수Temp")
                        col.Width = 90;
                    else if (header == "초기급수완료")
                        col.Width = 110;
                    else if (header == "초기급수중")
                        col.Width = 110;
                    else if (header == "물부족감지")
                        col.Width = 110;
                    else if (header == "버퍼부족")
                        col.Width = 100;
                    else if (header == "재가열")
                        col.Width = 90;
                    else if (header == "가열중")
                        col.Width = 90;
                    else if (header == "히터출력")
                        col.Width = 90;
                    else if (header == "컴프출력")
                        col.Width = 90;
                    else if (header == "온수밸브")
                        col.Width = 90;
                    else if (header == "냉수선택밸브")
                        col.Width = 110;
                    else if (header == "출수밸브")
                        col.Width = 90;
                    else if (header == "상태A")
                        col.Width = 200;
                    else if (header == "상태B")
                        col.Width = 200;
                    else if (header == "버튼정보")
                        col.Width = 200;
                    else
                        col.Width = DataGridLength.SizeToHeader;
                }
            }
        }

        private static DataTable BuildReceiveExportTable(DataTable source)
        {
            var export = new DataTable("receive_export");

            var visibleSpecs = new List<GridColumnSpec>();
            foreach (var spec in ReceiveColumnSpecs)
            {
                if (!spec.Visible) continue;
                if (!source.Columns.Contains(spec.ColumnName)) continue;
                visibleSpecs.Add(spec);
            }

            visibleSpecs.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

            foreach (var spec in visibleSpecs)
                export.Columns.Add(spec.Header, typeof(string));

            foreach (DataRow srcRow in source.Rows)
            {
                var newRow = export.NewRow();
                for (int i = 0; i < visibleSpecs.Count; i++)
                {
                    object value = srcRow[visibleSpecs[i].ColumnName];
                    newRow[i] = value == null || value == DBNull.Value
                        ? ""
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                export.Rows.Add(newRow);
            }

            return export;
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

        private static int? GetComboInt(ComboBox cb)
        {
            if (cb?.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag) && int.TryParse(tag, out int v))
                    return v;
            }
            return null;
        }

        private static int? TryParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s.Trim(), out int v) ? v : (int?)null;
        }

        private sealed class GridColumnSpec
        {
            public string ColumnName { get; set; }
            public string Header { get; set; }
            public bool Visible { get; set; } = true;
            public int DisplayIndex { get; set; } = int.MaxValue;
            public double Width { get; set; } = double.NaN;
        }

        private static readonly List<GridColumnSpec> ReceiveColumnSpecs = new List<GridColumnSpec>
        {
            new GridColumnSpec { ColumnName = "created_at", Header = "수신시각", DisplayIndex = 0, Width = 150 },
            new GridColumnSpec { ColumnName = "source_type_text", Header = "Source", DisplayIndex = 1, Width = 90 },
            new GridColumnSpec { ColumnName = "channel_no", Header = "채널번호", DisplayIndex = 2, Width = 80 , Visible = false},
            new GridColumnSpec { ColumnName = "model_name", Header = "모델명", DisplayIndex = 3, Width = 100 },
            new GridColumnSpec { ColumnName = "error_text", Header = "에러해석", DisplayIndex = 4, Width = 140 },

            new GridColumnSpec { ColumnName = "mode_selected_text", Header = "선택모드", DisplayIndex = 5, Width = 100 },
            new GridColumnSpec { ColumnName = "qty_selected_text", Header = "선택용량", DisplayIndex = 6, Width = 100 },
            new GridColumnSpec { ColumnName = "dispense_phase_text", Header = "출수단계", DisplayIndex = 7, Width = 120 },
            new GridColumnSpec { ColumnName = "dispense_sub_phase_text", Header = "출수세부단계", DisplayIndex = 8, Width = 140 },

            new GridColumnSpec { ColumnName = "hot_temp_text", Header = "온수Temp", DisplayIndex = 9, Width = 90 },
            new GridColumnSpec { ColumnName = "cold_temp_text", Header = "냉수Temp", DisplayIndex = 10, Width = 90 },

            new GridColumnSpec { ColumnName = "water_init_done_text", Header = "초기급수완료", DisplayIndex = 11, Width = 110 },
            new GridColumnSpec { ColumnName = "water_init_go_text", Header = "초기급수중", DisplayIndex = 12, Width = 110 },
            new GridColumnSpec { ColumnName = "empty_detect_text", Header = "물부족감지", DisplayIndex = 13, Width = 110 },
            new GridColumnSpec { ColumnName = "buffer_low_text", Header = "버퍼부족", DisplayIndex = 14, Width = 100 },
            new GridColumnSpec { ColumnName = "reheat_running_text", Header = "재가열", DisplayIndex = 15, Width = 90 },
            new GridColumnSpec { ColumnName = "hot_ing_text", Header = "가열중", DisplayIndex = 16, Width = 90 },

            new GridColumnSpec { ColumnName = "heater_output_text", Header = "히터출력", DisplayIndex = 17, Width = 90 },
            new GridColumnSpec { ColumnName = "compressor_output_text", Header = "컴프출력", DisplayIndex = 18, Width = 90 },
            new GridColumnSpec { ColumnName = "hot_valve_output_text", Header = "온수밸브", DisplayIndex = 19, Width = 90 },
            new GridColumnSpec { ColumnName = "cold_select_output_text", Header = "냉수선택밸브", DisplayIndex = 20, Width = 110 },
            new GridColumnSpec { ColumnName = "outlet_valve_output_text", Header = "출수밸브", DisplayIndex = 21, Width = 90 },

            new GridColumnSpec { ColumnName = "status_a_text", Header = "상태A", DisplayIndex = 22, Width = 200 },
            new GridColumnSpec { ColumnName = "status_b_text", Header = "상태B", DisplayIndex = 23, Width = 200 },
            new GridColumnSpec { ColumnName = "button_info_text", Header = "버튼정보", DisplayIndex = 24, Width = 200 },

            new GridColumnSpec { ColumnName = "id", Header = "ID", Visible = false },
            new GridColumnSpec { ColumnName = "created_at_ms", Header = "수신시각(ms)", Visible = false },
            new GridColumnSpec { ColumnName = "source_type", Header = "Source(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "model_code", Header = "모델코드", Visible = false },
            new GridColumnSpec { ColumnName = "error_code", Header = "에러코드", Visible = false },
            new GridColumnSpec { ColumnName = "water_init_done", Header = "초기급수완료(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "water_init_go", Header = "초기급수중(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "empty_detect", Header = "물부족감지(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "buffer_low", Header = "버퍼부족(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "reheat_running", Header = "재가열(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "hot_ing", Header = "가열중(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "heater_pwm", Header = "히터PWM(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "night", Header = "야간(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "test_mode", Header = "테스트모드(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "mode_selected", Header = "선택모드(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "qty_selected", Header = "선택용량(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "dispense_phase", Header = "출수단계(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "dispense_sub_phase", Header = "출수세부단계(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "hot_temp_raw", Header = "온수Temp(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "cold_temp_raw", Header = "냉수Temp(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "float_low_stable", Header = "Float 안정(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "ball_top_full_stable", Header = "BallTop 안정(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "water_buf_full_stable", Header = "WaterBuf 안정(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "heater_output", Header = "히터출력(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "compressor_output", Header = "컴프출력(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "hot_valve_output", Header = "온수밸브(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "cold_select_output", Header = "냉수선택밸브(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "outlet_valve_output", Header = "출수밸브(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "button_info", Header = "버튼정보(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "status_a", Header = "상태A(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "status_b", Header = "상태B(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "command", Header = "명령", Visible = false },
            new GridColumnSpec { ColumnName = "payload_size", Header = "Payload 크기", Visible = false },
            new GridColumnSpec { ColumnName = "checksum", Header = "체크섬", Visible = false },
            new GridColumnSpec { ColumnName = "end_packet", Header = "종료패킷", Visible = false }
        };

        private static GridColumnSpec FindReceiveColumnSpec(string columnName)
        {
            foreach (var spec in ReceiveColumnSpecs)
            {
                if (string.Equals(spec.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    return spec;
            }
            return null;
        }


        private static void AddReceiveInterpretColumns(DataTable dt)
        {
            if (dt == null) return;

            AddColumnIfMissing(dt, "source_type_text");
            AddColumnIfMissing(dt, "model_name");
            AddColumnIfMissing(dt, "error_text");
            AddColumnIfMissing(dt, "water_init_done_text");
            AddColumnIfMissing(dt, "water_init_go_text");
            AddColumnIfMissing(dt, "empty_detect_text");
            AddColumnIfMissing(dt, "buffer_low_text");
            AddColumnIfMissing(dt, "reheat_running_text");
            AddColumnIfMissing(dt, "hot_ing_text");
            AddColumnIfMissing(dt, "heater_pwm_text");
            AddColumnIfMissing(dt, "night_text");
            AddColumnIfMissing(dt, "test_mode_text");
            AddColumnIfMissing(dt, "mode_selected_text");
            AddColumnIfMissing(dt, "qty_selected_text");
            AddColumnIfMissing(dt, "dispense_phase_text");
            AddColumnIfMissing(dt, "dispense_sub_phase_text");
            AddColumnIfMissing(dt, "hot_temp_text");
            AddColumnIfMissing(dt, "cold_temp_text");
            AddColumnIfMissing(dt, "float_low_stable_text");
            AddColumnIfMissing(dt, "ball_top_full_stable_text");
            AddColumnIfMissing(dt, "water_buf_full_stable_text");
            AddColumnIfMissing(dt, "heater_output_text");
            AddColumnIfMissing(dt, "compressor_output_text");
            AddColumnIfMissing(dt, "hot_valve_output_text");
            AddColumnIfMissing(dt, "cold_select_output_text");
            AddColumnIfMissing(dt, "outlet_valve_output_text");
            AddColumnIfMissing(dt, "button_info_text");
            AddColumnIfMissing(dt, "status_a_text");
            AddColumnIfMissing(dt, "status_b_text");

            foreach (DataRow row in dt.Rows)
            {
                row["source_type_text"] = Duo8ValueText.GetSourceTypeText(ToInt(row["source_type"]));
                row["model_name"] = Duo8ValueText.GetModelName(ToInt(row["model_code"]));
                row["error_text"] = Duo8ValueText.GetErrorText((byte)ToInt(row["error_code"]));
                row["water_init_done_text"] = Duo8ValueText.ToDoneText((byte)ToInt(row["water_init_done"]));
                row["water_init_go_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["water_init_go"]));
                row["empty_detect_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["empty_detect"]));
                row["buffer_low_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["buffer_low"]));
                row["reheat_running_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["reheat_running"]));
                row["hot_ing_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["hot_ing"]));
                row["heater_pwm_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["heater_pwm"]));
                row["night_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["night"]));
                row["test_mode_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["test_mode"]));
                row["mode_selected_text"] = Duo8ValueText.GetModeText((byte)ToInt(row["mode_selected"]));
                row["qty_selected_text"] = Duo8ValueText.GetQtyText((byte)ToInt(row["qty_selected"]));
                row["dispense_phase_text"] = Duo8ValueText.GetDispensePhaseText((byte)ToInt(row["dispense_phase"]));
                row["dispense_sub_phase_text"] = Duo8ValueText.GetDispenseSubPhaseText((byte)ToInt(row["dispense_sub_phase"]));
                row["hot_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["hot_temp_raw"]));
                row["cold_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["cold_temp_raw"]));
                row["float_low_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["float_low_stable"]));
                row["ball_top_full_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["ball_top_full_stable"]));
                row["water_buf_full_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["water_buf_full_stable"]));
                row["heater_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["heater_output"]));
                row["compressor_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["compressor_output"]));
                row["hot_valve_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["hot_valve_output"]));
                row["cold_select_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["cold_select_output"]));
                row["outlet_valve_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["outlet_valve_output"]));
                row["button_info_text"] = Duo8ValueText.DecodeButtonInfo((ushort)ToInt(row["button_info"]));
                row["status_a_text"] = Duo8ValueText.DecodeStatusA((byte)ToInt(row["status_a"]));
                row["status_b_text"] = Duo8ValueText.DecodeStatusB((byte)ToInt(row["status_b"]));
            }
        }

        private static void AddColumnIfMissing(DataTable dt, string columnName)
        {
            if (!dt.Columns.Contains(columnName))
                dt.Columns.Add(columnName, typeof(string));
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
