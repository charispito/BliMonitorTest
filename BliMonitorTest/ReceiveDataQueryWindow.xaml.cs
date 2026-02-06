using BliMonitorTest.controls;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Input;
using System.Linq; // 꼭 추가

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

        public ReceiveDataQueryWindow()
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

        private void SetBusyMessage(bool busy, string title, string message) => SetBusy(busy);

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _isBusy) return;
            if (!_excelBuilding && !string.IsNullOrEmpty(_excelTempPath))
                ResetExcelState(deleteTempFile: true);
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

        private async void First_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; _page = 1; await RefreshGridAsync(); }
        private async void Prev_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; if (_page > 1) _page--; await RefreshGridAsync(); }
        private async void Next_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; int last = GetLastPage(); if (_page < last) _page++; await RefreshGridAsync(); }
        private async void Last_Click(object sender, RoutedEventArgs e) { if (_isBusy) return; _page = GetLastPage(); await RefreshGridAsync(); }

        private int GetLastPage()
        {
            if (_pageSize <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));
        }

        // ===== 공통 헬퍼 =====

        private int? GetComboInt(ComboBox cb)
        {
            if (cb?.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && int.TryParse(tag, out int v))
                    return v;
            }
            return null;
        }

        private int? TryParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s.Trim(), out int v) ? v : (int?)null;
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

        // ===== WHERE 생성 =====

        private static string BuildWhere(
            long fromMs, long toMs,
            int? modelCode, int? swVer,
            int sourceType, int? channelNo,
            int? heaterMin, int? heaterMax,
            int? coldMin, int? coldMax,
            int? compOn,
            int? waterLevelLow, int? floorSensor, int? uvLed,
            int? triSol1, int? triSol2, int? triSol3,
            int? airVentSol, int? cvSol,
            int? pumpOn, int? coldSol, int? normalSol, int? hotSol1,
            int? needleState)
        {
            var where = "WHERE created_at_ms >= @fromMs AND created_at_ms < @toMs";
            where += " AND (@sourceType = 0 OR source_type = @sourceType)";
            where += " AND (@channelNo = 0 OR channel_no = @channelNo)";

            if (modelCode.HasValue) where += " AND model_no = @modelCode";
            if (swVer.HasValue) where += " AND sw_ver = @swVer";

            if (heaterMin.HasValue) where += " AND heater_temp >= @heaterMin";
            if (heaterMax.HasValue) where += " AND heater_temp <= @heaterMax";

            if (coldMin.HasValue) where += " AND cold_temp >= @coldMin";
            if (coldMax.HasValue) where += " AND cold_temp <= @coldMax";

            if (compOn.HasValue) where += " AND Compressor = @compOn";

            if (waterLevelLow.HasValue) where += " AND low_water_sensor = @waterLevelLow";
            if (floorSensor.HasValue) where += " AND floor_sensor = @floorSensor";
            if (uvLed.HasValue) where += " AND uv_led = @uvLed";

            if (triSol1.HasValue) where += " AND sol_3way1 = @triSol1";
            if (triSol2.HasValue) where += " AND sol_3way2 = @triSol2";
            if (triSol3.HasValue) where += " AND sol_3way3 = @triSol3";

            if (airVentSol.HasValue) where += " AND air_vent_sol = @airVentSol";
            if (cvSol.HasValue) where += " AND cv_sol = @cvSol";

            if (pumpOn.HasValue) where += " AND pump = @pumpOn";
            if (coldSol.HasValue) where += " AND cold_sol = @coldSol";
            if (normalSol.HasValue) where += " AND normal_sol = @normalSol";
            if (hotSol1.HasValue) where += " AND hot_sol1 = @hotSol1";

            if (needleState.HasValue) where += " AND needle_pos = @needleState";

            return where;
        }

        private static void BindParams(SqliteCommand cmd,
            long fromMs, long toMs,
            int? modelCode, int? swVer,
            int sourceType, int? channelNo,
            int? heaterMin, int? heaterMax,
            int? coldMin, int? coldMax,
            int? compOn,
            int? waterLevelLow, int? floorSensor, int? uvLed,
            int? triSol1, int? triSol2, int? triSol3,
            int? airVentSol, int? cvSol,
            int? pumpOn, int? coldSol, int? normalSol, int? hotSol1,
            int? needleState)
        {
            cmd.Parameters.AddWithValue("@fromMs", fromMs);
            cmd.Parameters.AddWithValue("@toMs", toMs);
            cmd.Parameters.AddWithValue("@sourceType", sourceType);
            cmd.Parameters.AddWithValue("@channelNo", channelNo ?? 0);

            if (modelCode.HasValue) cmd.Parameters.AddWithValue("@modelCode", modelCode.Value);
            if (swVer.HasValue) cmd.Parameters.AddWithValue("@swVer", swVer.Value);

            if (heaterMin.HasValue) cmd.Parameters.AddWithValue("@heaterMin", heaterMin.Value);
            if (heaterMax.HasValue) cmd.Parameters.AddWithValue("@heaterMax", heaterMax.Value);

            if (coldMin.HasValue) cmd.Parameters.AddWithValue("@coldMin", coldMin.Value);
            if (coldMax.HasValue) cmd.Parameters.AddWithValue("@coldMax", coldMax.Value);

            if (compOn.HasValue) cmd.Parameters.AddWithValue("@compOn", compOn.Value);

            if (waterLevelLow.HasValue) cmd.Parameters.AddWithValue("@waterLevelLow", waterLevelLow.Value);
            if (floorSensor.HasValue) cmd.Parameters.AddWithValue("@floorSensor", floorSensor.Value);
            if (uvLed.HasValue) cmd.Parameters.AddWithValue("@uvLed", uvLed.Value);

            if (triSol1.HasValue) cmd.Parameters.AddWithValue("@triSol1", triSol1.Value);
            if (triSol2.HasValue) cmd.Parameters.AddWithValue("@triSol2", triSol2.Value);
            if (triSol3.HasValue) cmd.Parameters.AddWithValue("@triSol3", triSol3.Value);

            if (airVentSol.HasValue) cmd.Parameters.AddWithValue("@airVentSol", airVentSol.Value);
            if (cvSol.HasValue) cmd.Parameters.AddWithValue("@cvSol", cvSol.Value);

            if (pumpOn.HasValue) cmd.Parameters.AddWithValue("@pumpOn", pumpOn.Value);
            if (coldSol.HasValue) cmd.Parameters.AddWithValue("@coldSol", coldSol.Value);
            if (normalSol.HasValue) cmd.Parameters.AddWithValue("@normalSol", normalSol.Value);
            if (hotSol1.HasValue) cmd.Parameters.AddWithValue("@hotSol1", hotSol1.Value);

            if (needleState.HasValue) cmd.Parameters.AddWithValue("@needleState", needleState.Value);
        }

        // ===== 결과 조회 =====
        private async Task RefreshGridAsync()
        {
            if (!IsLoaded || _isBusy || grid == null || txtPageInfo == null) return;

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

            int? modelCode = GetComboInt(cbModelCode);
            int? swVer = GetComboInt(cbSwVer);
            int sourceType = 0;
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);
            int? channelNo = TryParseNullableInt(tbChannelNo.Text);

            int? heaterMin = TryParseNullableInt(tbHeaterMin.Text);
            int? heaterMax = TryParseNullableInt(tbHeaterMax.Text);
            int? coldMin = TryParseNullableInt(tbColdMin.Text);
            int? coldMax = TryParseNullableInt(tbColdMax.Text);
            int? compOn = GetComboInt(cbCompOn);

            int? lowWaterSensor = GetComboInt(cbLowWaterSensor);
            int? floorSensor = GetComboInt(cbFloorSensor);
            int? uvLed = GetComboInt(cbUvLed);
            int? triSol1 = GetComboInt(cbTriSol1);
            int? triSol2 = GetComboInt(cbTriSol2);
            int? triSol3 = GetComboInt(cbTriSol3);
            int? airVentSol = GetComboInt(cbAirVentSol);
            int? cvSol = GetComboInt(cbCvSol);
            int? pumpOn = GetComboInt(cbPumpOn);
            int? coldSol = GetComboInt(cbColdSol);
            int? normalSol = GetComboInt(cbNormalSol);
            int? hotSol1 = GetComboInt(cbHotSol1);
            int? needleState = GetComboInt(cbNeedleState);

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
                var result = await Task.Run(() =>
                {
                    string cs = $"Data Source={dbPath};";
                    using (var con = new SqliteConnection(cs))
                    {
                        con.Open();

                        string where = BuildWhere(
                            fromMs, toMs,
                            modelCode, swVer, sourceType, channelNo,
                            heaterMin, heaterMax,
                            coldMin, coldMax,
                            compOn,
                            lowWaterSensor, floorSensor, uvLed,
                            triSol1, triSol2, triSol3,
                            airVentSol, cvSol,
                            pumpOn, coldSol, normalSol, hotSol1,
                            needleState
                        );

                        int totalCount;
                        using (var cmdCount = con.CreateCommand())
                        {
                            cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                            BindParams(cmdCount,
                                fromMs, toMs,
                                modelCode, swVer, sourceType, channelNo,
                                heaterMin, heaterMax,
                                coldMin, coldMax,
                                compOn,
                                lowWaterSensor, floorSensor, uvLed,
                                triSol1, triSol2, triSol3,
                                airVentSol, cvSol,
                                pumpOn, coldSol, normalSol, hotSol1,
                                needleState
                            );
                            totalCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        }

                        int lastPage = (pageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                        int appliedPage = Math.Min(Math.Max(1, requestedPage), lastPage);
                        int offset = (appliedPage - 1) * pageSize;

                        var dt = new DataTable();
                        using (var cmd = con.CreateCommand())
                        {
                            cmd.CommandText =
                                "SELECT " +
                                "  id, source_type, channel_no, created_at, created_at_ms, " +
                                "  model_no, sw_ver, " +
                                "  heater_temp, " +
                                "  cold_temp, " +
                                "  Compressor, " +
                                "  low_water_sensor, " +
                                "  floor_sensor, " +
                                "  uv_led, " +
                                "  sol_3way1, sol_3way2, sol_3way3, " +
                                "  air_vent_sol, cv_sol, " +
                                "  pump, cold_sol, normal_sol, hot_sol1, " +
                                "  needle_pos, " +
                                "  command, payload_size, button_flags, checksum, end_packet " +
                                "FROM receive_data " +
                                where +
                                " ORDER BY created_at_ms DESC " +
                                " LIMIT @limit OFFSET @offset;";
                            BindParams(cmd,
                                fromMs, toMs,
                                modelCode, swVer, sourceType, channelNo,
                                heaterMin, heaterMax,
                                coldMin, coldMax,
                                compOn,
                                lowWaterSensor, floorSensor, uvLed,
                                triSol1, triSol2, triSol3,
                                airVentSol, cvSol,
                                pumpOn, coldSol, normalSol, hotSol1,
                                needleState
                            );
                            cmd.Parameters.AddWithValue("@limit", pageSize);
                            cmd.Parameters.AddWithValue("@offset", offset);

                            log.Debug(MonitoringDb.FormatSqlLog(cmd, "SELECT >> "));
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

        // ===== 엑셀(대용량) =====
        private void StartLargeExcelBuildInBackground(
            string dbPath, long fromMs, long toMs,
            int? modelCode, int? swVer, int sourceType, int? channelNo,
            int? heaterMin, int? heaterMax,
            int? coldMin, int? coldMax,
            int? compOn,
            int? waterLevelLow, int? floorSensor, int? uvLed,
            int? triSol1, int? triSol2, int? triSol3,
            int? airVentSol, int? cvSol,
            int? pumpOn, int? coldSol, int? normalSol, int? hotSol1,
            int? needleState,
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
                        dbPath, tmpPath,
                        fromMs, toMs,
                        modelCode, swVer, sourceType, channelNo,
                        heaterMin, heaterMax,
                        coldMin, coldMax,
                        compOn,
                        waterLevelLow, floorSensor, uvLed,
                        triSol1, triSol2, triSol3,
                        airVentSol, cvSol,
                        pumpOn, coldSol, normalSol, hotSol1,
                        needleState,
                        progress, token
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
                        MessageBox.Show(t.Exception?.GetBaseException()?.ToString() ?? "엑셀 생성 오류", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        // ===== 엑셀(스트리밍) =====
        private static void ExportAllToExcelOpenXml(
            string dbPath, string xlsxPath,
            long fromMs, long toMs,
            int? modelCode, int? swVer, int sourceType, int? channelNo,
            int? heaterMin, int? heaterMax,
            int? coldMin, int? coldMax,
            int? compOn,
            int? waterLevelLow, int? floorSensor, int? uvLed,
            int? triSol1, int? triSol2, int? triSol3,
            int? airVentSol, int? cvSol,
            int? pumpOn, int? coldSol, int? normalSol, int? hotSol1,
            int? needleState,
            IProgress<(int percent, long done, long total)> progress,
            CancellationToken token)
        {
            using (var con = new SqliteConnection($"Data Source={dbPath};"))
            {
                con.Open();

                string where = BuildWhere(
                    fromMs, toMs,
                    modelCode, swVer, sourceType, channelNo,
                    heaterMin, heaterMax,
                    coldMin, coldMax,
                    compOn,
                    waterLevelLow, floorSensor, uvLed,
                    triSol1, triSol2, triSol3,
                    airVentSol, cvSol,
                    pumpOn, coldSol, normalSol, hotSol1,
                    needleState
                );

                long total;
                using (var cmdCount = con.CreateCommand())
                {
                    cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                    BindParams(cmdCount,
                        fromMs, toMs,
                        modelCode, swVer, sourceType, channelNo,
                        heaterMin, heaterMax,
                        coldMin, coldMax,
                        compOn,
                        waterLevelLow, floorSensor, uvLed,
                        triSol1, triSol2, triSol3,
                        airVentSol, cvSol,
                        pumpOn, coldSol, normalSol, hotSol1,
                        needleState
                    );
                    total = Convert.ToInt64(cmdCount.ExecuteScalar());
                }

                progress?.Report((0, 0, total));

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT " +
                        "  id, source_type, channel_no, created_at, created_at_ms, " +
                        "  model_no, sw_ver, " +
                        "  heater_temp, " +
                        "  cold_temp, " +
                        "  Compressor, " +
                        "  low_water_sensor, " +
                        "  floor_sensor, " +
                        "  uv_led, " +
                        "  sol_3way1, sol_3way2, sol_3way3, " +
                        "  air_vent_sol, cv_sol, " +
                        "  pump, cold_sol, normal_sol, hot_sol1, " +
                        "  needle_pos, " +
                        "  command, payload_size, button_flags, checksum, end_packet " +
                        "FROM receive_data " +
                        where +
                        " ORDER BY created_at_ms DESC;";
                    BindParams(cmd,
                        fromMs, toMs,
                        modelCode, swVer, sourceType, channelNo,
                        heaterMin, heaterMax,
                        coldMin, coldMax,
                        compOn,
                        waterLevelLow, floorSensor, uvLed,
                        triSol1, triSol2, triSol3,
                        airVentSol, cvSol,
                        pumpOn, coldSol, normalSol, hotSol1,
                        needleState
                    );

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
                        int currentRowInSheet = 0;
                        int sheetIndex = 1;

                        Action startNewSheet = () =>
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
                                Name = $"receive_data_{sheetIndex++}"
                            };
                            sheets.Append(sheet);

                            currentRowInSheet = 0;

                            WriteHeaderRow(writer, reader);
                            currentRowInSheet++;
                        };

                        startNewSheet();

                        while (reader.Read())
                        {
                            if (currentRowInSheet >= MaxRowsPerSheet)
                                startNewSheet();

                            WriteDataRow(writer, reader);
                            currentRowInSheet++;
                            written++;

                            if (total > 0 && (written % 500) == 0)
                            {
                                int percent = (int)(written * 100 / total);
                                progress?.Report((percent, written, total));
                            }
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
                InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(text ?? ""))
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
                    File.Copy(_excelTempPath, sfd2.FileName, overwrite: true);
                    ResetExcelState(deleteTempFile: true);
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

            int? modelCode = GetComboInt(cbModelCode);
            int? swVer = GetComboInt(cbSwVer);
            int sourceType = 0;
            if (cbSourceType?.SelectedItem is ComboBoxItem srcItem && srcItem.Tag != null)
                int.TryParse(srcItem.Tag.ToString(), out sourceType);
            int? channelNo = TryParseNullableInt(tbChannelNo.Text);

            int? heaterMin = TryParseNullableInt(tbHeaterMin.Text);
            int? heaterMax = TryParseNullableInt(tbHeaterMax.Text);
            int? coldMin = TryParseNullableInt(tbColdMin.Text);
            int? coldMax = TryParseNullableInt(tbColdMax.Text);
            int? compOn = GetComboInt(cbCompOn);

            int? lowWaterSensor = GetComboInt(cbLowWaterSensor);
            int? floorSensor = GetComboInt(cbFloorSensor);
            int? uvLed = GetComboInt(cbUvLed);
            int? triSol1 = GetComboInt(cbTriSol1);
            int? triSol2 = GetComboInt(cbTriSol2);
            int? triSol3 = GetComboInt(cbTriSol3);
            int? airVentSol = GetComboInt(cbAirVentSol);
            int? cvSol = GetComboInt(cbCvSol);
            int? pumpOn = GetComboInt(cbPumpOn);
            int? coldSol = GetComboInt(cbColdSol);
            int? normalSol = GetComboInt(cbNormalSol);
            int? hotSol1 = GetComboInt(cbHotSol1);
            int? needleState = GetComboInt(cbNeedleState);

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
                        fromMs, toMs,
                        modelCode, swVer, sourceType, channelNo,
                        heaterMin, heaterMax,
                        coldMin, coldMax,
                        compOn,
                        lowWaterSensor, floorSensor, uvLed,
                        triSol1, triSol2, triSol3,
                        airVentSol, cvSol,
                        pumpOn, coldSol, normalSol, hotSol1,
                        needleState
                    );

                    using (var cmdCount = con.CreateCommand())
                    {
                        cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                        BindParams(cmdCount,
                            fromMs, toMs,
                            modelCode, swVer, sourceType, channelNo,
                            heaterMin, heaterMax,
                            coldMin, coldMax,
                            compOn,
                            lowWaterSensor, floorSensor, uvLed,
                            triSol1, triSol2, triSol3,
                            airVentSol, cvSol,
                            pumpOn, coldSol, normalSol, hotSol1,
                            needleState
                        );
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
                    $"조건에 맞는 데이터가 {totalCount:N0}건입니다.\n" +
                    $"대용량은 엑셀 생성에 시간이 걸릴 수 있어 백그라운드에서 생성합니다.\n\n" +
                    $"지금 생성할까요?",
                    "대용량 엑셀 생성",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (res != MessageBoxResult.Yes) return;

                StartLargeExcelBuildInBackground(
                    dbPath, fromMs, toMs,
                    modelCode, swVer, sourceType, channelNo,
                    heaterMin, heaterMax,
                    coldMin, coldMax,
                    compOn,
                    lowWaterSensor, floorSensor, uvLed,
                    triSol1, triSol2, triSol3,
                    airVentSol, cvSol,
                    pumpOn, coldSol, normalSol, hotSol1,
                    needleState,
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
                        dbPath, sfd.FileName,
                        fromMs, toMs,
                        modelCode, swVer, sourceType, channelNo,
                        heaterMin, heaterMax,
                        coldMin, coldMax,
                        compOn,
                        lowWaterSensor, floorSensor, uvLed,
                        triSol1, triSol2, triSol3,
                        airVentSol, cvSol,
                        pumpOn, coldSol, normalSol, hotSol1,
                        needleState,
                        progress: null, token: CancellationToken.None
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

        private void Grid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            var snapshot = new List<DataGridColumn>(grid.Columns);
            var replacements = new List<(int index, DataGridColumn newCol)>();

            foreach (var col in snapshot)
            {
                col.CanUserSort = true;

                switch (col.Header?.ToString())
                {
                    case "Compressor": col.Header = "콤프레셔(ON/OFF)"; break;
                    case "uv_led": col.Header = "UV LED"; break;
                    case "command": col.Header = "Command"; break;
                    case "checksum": col.Header = "Checksum"; break;
                }

                if (col is DataGridTextColumn textCol)
                {
                    int idx = grid.Columns.IndexOf(textCol);

                    Binding b = textCol.Binding as Binding;

                    var template = new DataTemplate();
                    var f = new FrameworkElementFactory(typeof(TextBlock));
                    f.SetValue(TextBlock.StyleProperty, FindResource("CenterCellTextBlock"));
                    if (b != null)
                    {
                        f.SetBinding(TextBlock.TextProperty, b);
                        f.SetBinding(TextBlock.ToolTipProperty, b);
                    }
                    template.VisualTree = f;

                    var tplCol = new DataGridTemplateColumn
                    {
                        Header = textCol.Header,
                        SortMemberPath = textCol.SortMemberPath,
                        CellTemplate = template
                    };

                    replacements.Add((idx, tplCol));
                }
            }

            // 역순 교체: 분해 대신 Item1/Item2 사용
            foreach (var r in replacements.OrderByDescending(r => r.index))
            {
                grid.Columns.RemoveAt(r.index);
                grid.Columns.Insert(r.index, r.newCol);
            }
        }

    }
}
