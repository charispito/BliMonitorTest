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
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using log4net;
using Microsoft.Data.Sqlite;

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

        private sealed class QueryResult
        {
            public int TotalCount;
            public int AppliedPage;
            public int LastPage;
            public DataTable Table;
        }

        public ErrorDataQueryWindow()
        {
            _isInitializing = true;
            InitializeComponent();
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

        public void SetInitialFilter(int? sourceType, int? channelNo)
        {
            if (sourceType.HasValue)
            {
                foreach (var it in cbSourceType.Items)
                {
                    if (it is ComboBoxItem cbi && cbi.Tag?.ToString() == sourceType.Value.ToString())
                    {
                        cbi.IsSelected = true;
                        break;
                    }
                }
            }

            if (channelNo.HasValue)
                tbChannelNo.Text = channelNo.Value.ToString();
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            if (BusyOverlay != null)
                BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
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
            int? slotNo = TryParseNullableInt(tbErrorSlot.Text);
            int? errorCode = TryParseNullableInt(tbErrorCode.Text);
            int? validMark = GetComboInt(cbValidMark);
            int? waterInitDone = GetComboInt(cbWaterInitDone);
            int? bufferLow = GetComboInt(cbBufferLow);

            int? seqMin = TryParseNullableInt(tbSeqMin.Text);
            int? seqMax = TryParseNullableInt(tbSeqMax.Text);

            int? hotTempMin = TryParseNullableInt(tbHotTempMin.Text);
            int? hotTempMax = TryParseNullableInt(tbHotTempMax.Text);

            int? coldTempMin = TryParseNullableInt(tbColdTempMin.Text);
            int? coldTempMax = TryParseNullableInt(tbColdTempMax.Text);

            int? adcHotMin = TryParseNullableInt(tbAdcHotMin.Text);
            int? adcHotMax = TryParseNullableInt(tbAdcHotMax.Text);

            int? adcColdMin = TryParseNullableInt(tbAdcColdMin.Text);
            int? adcColdMax = TryParseNullableInt(tbAdcColdMax.Text);

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
                            sourceType,
                            channelNo,
                            slotNo,
                            errorCode,
                            validMark,
                            waterInitDone,
                            bufferLow,
                            seqMin,
                            seqMax,
                            hotTempMin,
                            hotTempMax,
                            coldTempMin,
                            coldTempMax,
                            adcHotMin,
                            adcHotMax,
                            adcColdMin,
                            adcColdMax
                        );

                        int totalCount;
                        using (var cmdCount = con.CreateCommand())
                        {
                            cmdCount.CommandText = "SELECT COUNT(1) FROM error_history " + where + ";";
                            BindParams(
                                cmdCount,
                                fromMs,
                                toMs,
                                sourceType,
                                channelNo,
                                slotNo,
                                errorCode,
                                validMark,
                                waterInitDone,
                                bufferLow,
                                seqMin,
                                seqMax,
                                hotTempMin,
                                hotTempMax,
                                coldTempMin,
                                coldTempMax,
                                adcHotMin,
                                adcHotMax,
                                adcColdMin,
                                adcColdMax
                            );

                            log.Info(MonitoringDb.FormatSqlLog(cmdCount, "ERROR_HISTORY COUNT : "));
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
                                " id, created_at, source_type, channel_no, request_command, response_command, slot_no, " +
                                " valid_mark, sequence_no, error_code, hot_temp_raw, cold_temp_raw, " +
                                " adc_hot_raw, adc_cold_raw, water_init_done, status_a, status_b, buffer_low, record_crc " +
                                "FROM error_history " +
                                where +
                                " ORDER BY id DESC " +
                                " LIMIT @limit OFFSET @offset;";

                            BindParams(
                                cmd,
                                fromMs,
                                toMs,
                                sourceType,
                                channelNo,
                                slotNo,
                                errorCode,
                                validMark,
                                waterInitDone,
                                bufferLow,
                                seqMin,
                                seqMax,
                                hotTempMin,
                                hotTempMax,
                                coldTempMin,
                                coldTempMax,
                                adcHotMin,
                                adcHotMax,
                                adcColdMin,
                                adcColdMax
                            );

                            cmd.Parameters.AddWithValue("@limit", pageSize);
                            cmd.Parameters.AddWithValue("@offset", offset);

                            log.Info(MonitoringDb.FormatSqlLog(cmd, "ERROR_HISTORY LIST : "));

                            using (var reader = cmd.ExecuteReader())
                                dt.Load(reader);
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

                AddErrorInterpretColumns(result.Table);

                var displayTable = BuildErrorExportTable(result.Table);

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
            int sourceType,
            int? channelNo,
            int? slotNo,
            int? errorCode,
            int? validMark,
            int? waterInitDone,
            int? bufferLow,
            int? seqMin,
            int? seqMax,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? adcHotMin,
            int? adcHotMax,
            int? adcColdMin,
            int? adcColdMax)
        {
            string where = "WHERE created_at_ms >= @fromMs AND created_at_ms < @toMs";
            where += " AND (@sourceType = 0 OR source_type = @sourceType)";
            where += " AND (@channelNo = 0 OR channel_no = @channelNo)";

            if (slotNo.HasValue)
                where += " AND slot_no = @slotNo";

            if (errorCode.HasValue)
                where += " AND error_code = @errorCode";

            if (validMark.HasValue)
                where += " AND valid_mark = @validMark";

            if (waterInitDone.HasValue)
                where += " AND water_init_done = @waterInitDone";

            if (bufferLow.HasValue)
                where += " AND buffer_low = @bufferLow";

            if (seqMin.HasValue)
                where += " AND sequence_no >= @seqMin";

            if (seqMax.HasValue)
                where += " AND sequence_no <= @seqMax";

            if (hotTempMin.HasValue)
                where += " AND hot_temp_raw >= @hotTempMin";

            if (hotTempMax.HasValue)
                where += " AND hot_temp_raw <= @hotTempMax";

            if (coldTempMin.HasValue)
                where += " AND cold_temp_raw >= @coldTempMin";

            if (coldTempMax.HasValue)
                where += " AND cold_temp_raw <= @coldTempMax";

            if (adcHotMin.HasValue)
                where += " AND adc_hot_raw >= @adcHotMin";

            if (adcHotMax.HasValue)
                where += " AND adc_hot_raw <= @adcHotMax";

            if (adcColdMin.HasValue)
                where += " AND adc_cold_raw >= @adcColdMin";

            if (adcColdMax.HasValue)
                where += " AND adc_cold_raw <= @adcColdMax";

            return where;
        }

        private static void BindParams(
            SqliteCommand cmd,
            long fromMs,
            long toMs,
            int sourceType,
            int? channelNo,
            int? slotNo,
            int? errorCode,
            int? validMark,
            int? waterInitDone,
            int? bufferLow,
            int? seqMin,
            int? seqMax,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? adcHotMin,
            int? adcHotMax,
            int? adcColdMin,
            int? adcColdMax)
        {
            cmd.Parameters.AddWithValue("@fromMs", fromMs);
            cmd.Parameters.AddWithValue("@toMs", toMs);
            cmd.Parameters.AddWithValue("@sourceType", sourceType);
            cmd.Parameters.AddWithValue("@channelNo", channelNo ?? 0);

            if (slotNo.HasValue)
                cmd.Parameters.AddWithValue("@slotNo", slotNo.Value);

            if (errorCode.HasValue)
                cmd.Parameters.AddWithValue("@errorCode", errorCode.Value);

            if (validMark.HasValue)
                cmd.Parameters.AddWithValue("@validMark", validMark.Value);

            if (waterInitDone.HasValue)
                cmd.Parameters.AddWithValue("@waterInitDone", waterInitDone.Value);

            if (bufferLow.HasValue)
                cmd.Parameters.AddWithValue("@bufferLow", bufferLow.Value);

            if (seqMin.HasValue)
                cmd.Parameters.AddWithValue("@seqMin", seqMin.Value);

            if (seqMax.HasValue)
                cmd.Parameters.AddWithValue("@seqMax", seqMax.Value);

            if (hotTempMin.HasValue)
                cmd.Parameters.AddWithValue("@hotTempMin", hotTempMin.Value);

            if (hotTempMax.HasValue)
                cmd.Parameters.AddWithValue("@hotTempMax", hotTempMax.Value);

            if (coldTempMin.HasValue)
                cmd.Parameters.AddWithValue("@coldTempMin", coldTempMin.Value);

            if (coldTempMax.HasValue)
                cmd.Parameters.AddWithValue("@coldTempMax", coldTempMax.Value);

            if (adcHotMin.HasValue)
                cmd.Parameters.AddWithValue("@adcHotMin", adcHotMin.Value);

            if (adcHotMax.HasValue)
                cmd.Parameters.AddWithValue("@adcHotMax", adcHotMax.Value);

            if (adcColdMin.HasValue)
                cmd.Parameters.AddWithValue("@adcColdMin", adcColdMin.Value);

            if (adcColdMax.HasValue)
                cmd.Parameters.AddWithValue("@adcColdMax", adcColdMax.Value);
        }

        private async void ExcelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || _excelBuilding) return;

            if (!string.IsNullOrEmpty(_excelTempPath) && File.Exists(_excelTempPath))
            {
                var sfd2 = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = $"error_history_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
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
            int? slotNo = TryParseNullableInt(tbErrorSlot.Text);
            int? errorCode = TryParseNullableInt(tbErrorCode.Text);
            int? validMark = GetComboInt(cbValidMark);
            int? waterInitDone = GetComboInt(cbWaterInitDone);
            int? bufferLow = GetComboInt(cbBufferLow);

            int? seqMin = TryParseNullableInt(tbSeqMin.Text);
            int? seqMax = TryParseNullableInt(tbSeqMax.Text);

            int? hotTempMin = TryParseNullableInt(tbHotTempMin.Text);
            int? hotTempMax = TryParseNullableInt(tbHotTempMax.Text);

            int? coldTempMin = TryParseNullableInt(tbColdTempMin.Text);
            int? coldTempMax = TryParseNullableInt(tbColdTempMax.Text);

            int? adcHotMin = TryParseNullableInt(tbAdcHotMin.Text);
            int? adcHotMax = TryParseNullableInt(tbAdcHotMax.Text);

            int? adcColdMin = TryParseNullableInt(tbAdcColdMin.Text);
            int? adcColdMax = TryParseNullableInt(tbAdcColdMax.Text);

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
                        sourceType,
                        channelNo,
                        slotNo,
                        errorCode,
                        validMark,
                        waterInitDone,
                        bufferLow,
                        seqMin,
                        seqMax,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        adcHotMin,
                        adcHotMax,
                        adcColdMin,
                        adcColdMax
                    );

                    using (var cmdCount = con.CreateCommand())
                    {
                        cmdCount.CommandText = "SELECT COUNT(1) FROM error_history " + where + ";";
                        BindParams(
                            cmdCount,
                            fromMs,
                            toMs,
                            sourceType,
                            channelNo,
                            slotNo,
                            errorCode,
                            validMark,
                            waterInitDone,
                            bufferLow,
                            seqMin,
                            seqMax,
                            hotTempMin,
                            hotTempMax,
                            coldTempMin,
                            coldTempMax,
                            adcHotMin,
                            adcHotMax,
                            adcColdMin,
                            adcColdMax
                        );

                        log.Info(MonitoringDb.FormatSqlLog(cmdCount, "ERROR_HISTORY EXCEL COUNT : "));
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
                    MessageBoxImage.Warning
                );

                if (res != MessageBoxResult.Yes) return;

                StartLargeExcelBuildInBackground(
                    dbPath,
                    fromMs,
                    toMs,
                    sourceType,
                    channelNo,
                    slotNo,
                    errorCode,
                    validMark,
                    waterInitDone,
                    bufferLow,
                    seqMin,
                    seqMax,
                    hotTempMin,
                    hotTempMax,
                    coldTempMin,
                    coldTempMax,
                    adcHotMin,
                    adcHotMax,
                    adcColdMin,
                    adcColdMax,
                    totalCount
                );
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"error_history_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            SetBusy(true);
            try
            {
                await Task.Run(() =>
                {
                    ExportAllToExcelOpenXml(
                        dbPath,
                        sfd.FileName,
                        fromMs,
                        toMs,
                        sourceType,
                        channelNo,
                        slotNo,
                        errorCode,
                        validMark,
                        waterInitDone,
                        bufferLow,
                        seqMin,
                        seqMax,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        adcHotMin,
                        adcHotMax,
                        adcColdMin,
                        adcColdMax,
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
            int sourceType,
            int? channelNo,
            int? slotNo,
            int? errorCode,
            int? validMark,
            int? waterInitDone,
            int? bufferLow,
            int? seqMin,
            int? seqMax,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? adcHotMin,
            int? adcHotMax,
            int? adcColdMin,
            int? adcColdMax,
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

            string tmpPath = Path.Combine(tempDir, $"error_history_{DateTime.Now:yyyyMMdd_HHmmss}.tmp.xlsx");
            string finalTempPath = Path.Combine(tempDir, $"error_history_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

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
                        sourceType,
                        channelNo,
                        slotNo,
                        errorCode,
                        validMark,
                        waterInitDone,
                        bufferLow,
                        seqMin,
                        seqMax,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        adcHotMin,
                        adcHotMax,
                        adcColdMin,
                        adcColdMax,
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
                            t.Exception?.GetBaseException().ToString() ?? "엑셀 생성 오류",
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
            int sourceType,
            int? channelNo,
            int? slotNo,
            int? errorCode,
            int? validMark,
            int? waterInitDone,
            int? bufferLow,
            int? seqMin,
            int? seqMax,
            int? hotTempMin,
            int? hotTempMax,
            int? coldTempMin,
            int? coldTempMax,
            int? adcHotMin,
            int? adcHotMax,
            int? adcColdMin,
            int? adcColdMax,
            IProgress<(int percent, long done, long total)> progress,
            CancellationToken token)
        {
            string where = BuildWhere(
                sourceType,
                channelNo,
                slotNo,
                errorCode,
                validMark,
                waterInitDone,
                bufferLow,
                seqMin,
                seqMax,
                hotTempMin,
                hotTempMax,
                coldTempMin,
                coldTempMax,
                adcHotMin,
                adcHotMax,
                adcColdMin,
                adcColdMax
            );

            using (var con = new SqliteConnection($"Data Source={dbPath};"))
            {
                con.Open();

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT " +
                        " id, created_at, source_type, channel_no, request_command, response_command, slot_no, " +
                        " valid_mark, sequence_no, error_code, hot_temp_raw, cold_temp_raw, " +
                        " adc_hot_raw, adc_cold_raw, water_init_done, status_a, status_b, buffer_low, record_crc " +
                        "FROM error_history " +
                        where +
                        " ORDER BY id DESC;";

                    BindParams(
                        cmd,
                        fromMs,
                        toMs,
                        sourceType,
                        channelNo,
                        slotNo,
                        errorCode,
                        validMark,
                        waterInitDone,
                        bufferLow,
                        seqMin,
                        seqMax,
                        hotTempMin,
                        hotTempMax,
                        coldTempMin,
                        coldTempMax,
                        adcHotMin,
                        adcHotMax,
                        adcColdMin,
                        adcColdMax
                    );

                    log.Info(MonitoringDb.FormatSqlLog(cmd, "ERROR_HISTORY EXPORT DATA : "));

                    var dt = new DataTable();
                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }

                    AddErrorInterpretColumns(dt);

                    var exportTable = BuildErrorExportTable(dt);

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
                                Name = $"error_history_{sheetIndex++}"
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

                    if (header == "생성시각")
                        col.Width = 150;
                    else if (header == "Source")
                        col.Width = 90;
                    else if (header == "채널번호")
                        col.Width = 80;
                    else if (header == "슬롯번호")
                        col.Width = 80;
                    else if (header == "SEQ")
                        col.Width = 80;
                    else if (header == "유효마크")
                        col.Width = 100;
                    else if (header == "에러해석")
                        col.Width = 140;
                    else if (header == "온수Temp")
                        col.Width = 90;
                    else if (header == "냉수Temp")
                        col.Width = 90;
                    else if (header == "ADC HOT")
                        col.Width = 90;
                    else if (header == "ADC COLD")
                        col.Width = 90;
                    else if (header == "초기급수완료")
                        col.Width = 120;
                    else if (header == "버퍼부족")
                        col.Width = 100;
                    else if (header == "상태A")
                        col.Width = 220;
                    else if (header == "상태B")
                        col.Width = 220;
                    else
                        col.Width = DataGridLength.SizeToHeader;
                }
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

        private static readonly List<GridColumnSpec> ErrorColumnSpecs = new List<GridColumnSpec>
        {
            new GridColumnSpec { ColumnName = "created_at", Header = "생성시각", DisplayIndex = 0, Width = 150 },
            new GridColumnSpec { ColumnName = "source_type_text", Header = "Source", DisplayIndex = 1, Width = 90 },
            new GridColumnSpec { ColumnName = "channel_no", Header = "채널번호", DisplayIndex = 2, Width = 80 , Visible = false},
            new GridColumnSpec { ColumnName = "slot_no", Header = "슬롯번호", DisplayIndex = 3, Width = 80 },
            new GridColumnSpec { ColumnName = "sequence_no", Header = "SEQ", DisplayIndex = 4, Width = 80 },
            new GridColumnSpec { ColumnName = "valid_mark_text", Header = "유효마크", DisplayIndex = 5, Width = 100 },
            new GridColumnSpec { ColumnName = "error_text", Header = "에러해석", DisplayIndex = 6, Width = 140 },
            new GridColumnSpec { ColumnName = "hot_temp_text", Header = "온수Temp", DisplayIndex = 7, Width = 90 },
            new GridColumnSpec { ColumnName = "cold_temp_text", Header = "냉수Temp", DisplayIndex = 8, Width = 90 },
            new GridColumnSpec { ColumnName = "adc_hot_text", Header = "ADC HOT", DisplayIndex = 9, Width = 90 },
            new GridColumnSpec { ColumnName = "adc_cold_text", Header = "ADC COLD", DisplayIndex = 10, Width = 90 },
            new GridColumnSpec { ColumnName = "water_init_done_text", Header = "초기급수완료", DisplayIndex = 11, Width = 120 },
            new GridColumnSpec { ColumnName = "buffer_low_text", Header = "버퍼부족", DisplayIndex = 12, Width = 100 },
            new GridColumnSpec { ColumnName = "status_a_text", Header = "상태A", DisplayIndex = 13, Width = 220 },
            new GridColumnSpec { ColumnName = "status_b_text", Header = "상태B", DisplayIndex = 14, Width = 220 },

            new GridColumnSpec { ColumnName = "id", Header = "ID", Visible = false },
            new GridColumnSpec { ColumnName = "source_type", Header = "Source(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "request_command", Header = "요청명령", Visible = false },
            new GridColumnSpec { ColumnName = "response_command", Header = "응답명령", Visible = false },
            new GridColumnSpec { ColumnName = "valid_mark", Header = "유효마크(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "error_code", Header = "에러코드(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "hot_temp_raw", Header = "온수Temp(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "cold_temp_raw", Header = "냉수Temp(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "adc_hot_raw", Header = "ADC HOT(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "adc_cold_raw", Header = "ADC COLD(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "water_init_done", Header = "초기급수완료(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "status_a", Header = "상태A(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "status_b", Header = "상태B(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "buffer_low", Header = "버퍼부족(raw)", Visible = false },
            new GridColumnSpec { ColumnName = "record_crc", Header = "레코드CRC", Visible = false }
        };

        private static GridColumnSpec FindErrorColumnSpec(string columnName)
        {
            foreach (var spec in ErrorColumnSpecs)
            {
                if (string.Equals(spec.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    return spec;
            }
            return null;
        }

        private static DataTable BuildErrorExportTable(DataTable source)
        {
            var export = new DataTable("error_export");

            var visibleSpecs = new List<GridColumnSpec>();
            foreach (var spec in ErrorColumnSpecs)
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

        private static void AddErrorInterpretColumns(DataTable dt)
        {
            if (dt == null) return;

            AddColumnIfMissing(dt, "source_type_text");
            AddColumnIfMissing(dt, "valid_mark_text");
            AddColumnIfMissing(dt, "error_text");
            AddColumnIfMissing(dt, "hot_temp_text");
            AddColumnIfMissing(dt, "cold_temp_text");
            AddColumnIfMissing(dt, "adc_hot_text");
            AddColumnIfMissing(dt, "adc_cold_text");
            AddColumnIfMissing(dt, "water_init_done_text");
            AddColumnIfMissing(dt, "buffer_low_text");
            AddColumnIfMissing(dt, "status_a_text");
            AddColumnIfMissing(dt, "status_b_text");

            foreach (DataRow row in dt.Rows)
            {
                row["source_type_text"] = Duo8ValueText.GetSourceTypeText(ToInt(row["source_type"]));
                row["valid_mark_text"] = Duo8ValueText.GetValidMarkText(ToInt(row["valid_mark"]));
                row["error_text"] = Duo8ValueText.GetErrorText((byte)ToInt(row["error_code"]));
                row["hot_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["hot_temp_raw"]));
                row["cold_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["cold_temp_raw"]));
                row["adc_hot_text"] = Duo8ValueText.FormatRawUShort((ushort)ToInt(row["adc_hot_raw"]));
                row["adc_cold_text"] = Duo8ValueText.FormatRawUShort((ushort)ToInt(row["adc_cold_raw"]));
                row["water_init_done_text"] = Duo8ValueText.ToDoneText((byte)ToInt(row["water_init_done"]));
                row["buffer_low_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["buffer_low"]));
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
