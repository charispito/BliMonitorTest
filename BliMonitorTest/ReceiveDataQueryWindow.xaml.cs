using BliMonitorTest.controls;
using BliMonitorTest.util.MonitoringDb;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BliMonitorTest
{
    public partial class ReceiveDataQueryWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ReceiveDataQueryWindow));

        private int _page = 1;
        private int _pageSize = 50;
        private int _totalCount = 0;

        // 초기화 및 상태
        private bool _isInitializing = true;
        private bool _isBusy = false;

        // Excel 관련
        private const long ExcelLargeThreshold = 100000; // ✅ 대용량 기준 (추천)
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

        private void SetBusyMessage(bool busy, string title, string message)
        {
            // 현재 BusyOverlay는 텍스트 컨트롤 이름이 없어서 title/message는 일단 무시하고
            // Busy 상태만 적용합니다. (나중에 BusyOverlay 텍스트에 x:Name 붙이면 반영 가능)
            SetBusy(busy);
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            if (_isBusy) return;

            // ✅ 새 조회를 시작하면, 이전 대용량 엑셀 준비 상태는 폐기(단발성 정책)
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

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            int last = GetLastPage();
            if (_page < last) _page++;
            await RefreshGridAsync();
        }

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

            // ✅ UI 컨트롤 값은 "여기(UI 스레드)"에서만 읽고
            DateTime from = (dpFrom.SelectedDate ?? DateTime.Today).Date;
            DateTime to = (dpTo.SelectedDate ?? DateTime.Today).Date;
            DateTime toExclusive = to.AddDays(1);

            if (toExclusive <= from)
            {
                MessageBox.Show("기간이 올바르지 않습니다.");
                return;
            }

            if ((toExclusive - from).TotalDays > 31)
            {
                MessageBox.Show("기간 조회는 최대 1달(31일)까지만 가능합니다.");
                return;
            }

            int? mode = TryParseNullableInt(tbMode.Text);
            double? heaterMin = TryParseNullableDouble(tbHeaterMin.Text);
            double? heaterMax = TryParseNullableDouble(tbHeaterMax.Text);
            double? airMin = TryParseNullableDouble(tbAirMin.Text);
            double? airMax = TryParseNullableDouble(tbAirMax.Text);
            int? motor = TryParseNullableInt(tbMotor.Text);
            int? fanSpeed = TryParseNullableInt(tbFanSpeed.Text);
            double? motorCurrentMin = TryParseNullableDouble(tbMotorCurrentMin.Text);

            string fromText = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string toText = toExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Monitoring.db");
            if (!File.Exists(dbPath))
            {
                MessageBox.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                return;
            }

            int requestedPage = _page;
            int pageSize = _pageSize;

            SetBusy(true);

            try
            {
                // ✅ DB 조회는 백그라운드에서 수행
                QueryResult result = await Task.Run(() =>
                {
                    string cs = $"Data Source={dbPath};";

                    using (var con = new SqliteConnection(cs))
                    {
                        con.Open();

                        // WHERE 동적 구성
                        string where = "WHERE created_at >= @from AND created_at < @to";
                        if (mode.HasValue) where += " AND mode = @mode";
                        if (heaterMin.HasValue) where += " AND heater_temp >= @heaterMin";
                        if (heaterMax.HasValue) where += " AND heater_temp <= @heaterMax";
                        if (airMin.HasValue) where += " AND air_temp >= @airMin";
                        if (airMax.HasValue) where += " AND air_temp <= @airMax";
                        if (motor.HasValue) where += " AND motor = @motor";
                        if (fanSpeed.HasValue) where += " AND fan_speed = @fanSpeed";
                        if (motorCurrentMin.HasValue) where += " AND motor_current >= @motorCurrentMin";

                        // 1) COUNT
                        int totalCount;
                        using (var cmdCount = con.CreateCommand())
                        {
                            cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                            BindParams(cmdCount, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                            totalCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        }

                        int lastPage = (pageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                        int appliedPage = Math.Min(Math.Max(1, requestedPage), lastPage);
                        int offset = (appliedPage - 1) * pageSize;

                        // 2) SELECT page
                        var dt = new DataTable();
                        using (var cmd = con.CreateCommand())
                        {
                            cmd.CommandText =
                                "SELECT * FROM receive_data " +
                                where +
                                " ORDER BY datetime(created_at) DESC " +
                                " LIMIT @limit OFFSET @offset;";

                            BindParams(cmd, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                            cmd.Parameters.AddWithValue("@limit", pageSize);
                            cmd.Parameters.AddWithValue("@offset", offset);

                            // 로그(백그라운드에서 찍어도 OK)
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

                // ✅ 여기부터는 다시 UI 스레드
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
        private static void BindParams( SqliteCommand cmd, string fromText, string toText, int? mode, double? heaterMin, double? heaterMax, double? airMin, double? airMax, int? motor, int? fanSpeed, double? motorCurrentMin)
        {
            cmd.Parameters.AddWithValue("@from", fromText);
            cmd.Parameters.AddWithValue("@to", toText);

            if (mode.HasValue) cmd.Parameters.AddWithValue("@mode", mode.Value);
            if (heaterMin.HasValue) cmd.Parameters.AddWithValue("@heaterMin", heaterMin.Value);
            if (heaterMax.HasValue) cmd.Parameters.AddWithValue("@heaterMax", heaterMax.Value);
            if (airMin.HasValue) cmd.Parameters.AddWithValue("@airMin", airMin.Value);
            if (airMax.HasValue) cmd.Parameters.AddWithValue("@airMax", airMax.Value);
            if (motor.HasValue) cmd.Parameters.AddWithValue("@motor", motor.Value);
            if (fanSpeed.HasValue) cmd.Parameters.AddWithValue("@fanSpeed", fanSpeed.Value);
            if (motorCurrentMin.HasValue) cmd.Parameters.AddWithValue("@motorCurrentMin", motorCurrentMin.Value);
        }

        private int? TryParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            int v;
            if (int.TryParse(s.Trim(), out v))
                return v;

            return null;
        }

        private double? TryParseNullableDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            double v;
            return double.TryParse(s.Trim(), out v) ? (double?)v : null;
        }

        private async void ExcelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;           // 조회 Busy와 충돌 방지(원하면 분리 가능)
            if (_excelBuilding) return;    // 생성 중 중복 클릭 방지

            // ✅ 이미 temp가 완성돼 있다면: 저장/열기 단계로
            if (!string.IsNullOrEmpty(_excelTempPath) && File.Exists(_excelTempPath))
            {
                var sfd2 = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = $"receive_data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (sfd2.ShowDialog() != true)
                    return; // 사용자가 취소하면 "엑셀 저장" 상태 유지(원하면 여기서 Reset도 가능)

                try
                {
                    File.Copy(_excelTempPath, sfd2.FileName, overwrite: true);

                    // ✅ 1회 저장 성공 → 초기 상태로 복귀(단발성)
                    ResetExcelState(deleteTempFile: true);

                    MessageBox.Show("엑셀 파일 저장이 완료되었습니다.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "엑셀 저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            // ✅ UI에서 검색조건 캡처 (백그라운드에서 UI 접근 금지)
            DateTime from = (dpFrom.SelectedDate ?? DateTime.Today).Date;
            DateTime to = (dpTo.SelectedDate ?? DateTime.Today).Date;
            DateTime toExclusive = to.AddDays(1);

            int? mode = TryParseNullableInt(tbMode.Text);
            double? heaterMin = TryParseNullableDouble(tbHeaterMin.Text);
            double? heaterMax = TryParseNullableDouble(tbHeaterMax.Text);
            double? airMin = TryParseNullableDouble(tbAirMin.Text);
            double? airMax = TryParseNullableDouble(tbAirMax.Text);
            int? motor = TryParseNullableInt(tbMotor.Text);
            int? fanSpeed = TryParseNullableInt(tbFanSpeed.Text);
            double? motorCurrentMin = TryParseNullableDouble(tbMotorCurrentMin.Text);

            string fromText = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string toText = toExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Monitoring.db");
            if (!File.Exists(dbPath))
            {
                MessageBox.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                return;
            }

            // 1) COUNT로 건수 측정
            long totalCount = await Task.Run(() =>
            {
                string cs = $"Data Source={dbPath};";
                using (var con = new SqliteConnection(cs))
                {
                    con.Open();
                    string where = BuildWhere(mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);

                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                        BindParams(cmd, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                        return Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }
            });

            if (totalCount <= 0)
            {
                MessageBox.Show("조건에 맞는 데이터가 없습니다.");
                return;
            }

            // 2) 대용량 판단
            if (totalCount >= ExcelLargeThreshold)
            {
                var res = MessageBox.Show( $"조건에 맞는 데이터가 {totalCount:N0}건입니다.\n" + $"대용량은 엑셀 생성에 시간이 걸릴 수 있어 백그라운드에서 생성합니다.\n\n" + $"지금 생성할까요?", "대용량 엑셀 생성", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (res != MessageBoxResult.Yes) return;

                // 3) TEMP에 백그라운드 생성 시작
                StartLargeExcelBuildInBackground( dbPath, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin, totalCount);

                return;
            }

            // 4) 저용량: 바로 저장(여기서는 스트리밍/ClosedXML 중 택1 가능)
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
                    // ✅ 저용량이면 ClosedXML로 예쁘게 / 또는 OpenXML로 통일도 가능
                    // (여기서는 “대용량용 OpenXML 스트리밍 함수”를 재사용하는 게 단순)
                    ExportAllToExcelOpenXml( dbPath, sfd.FileName, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin, progress: null, token: CancellationToken.None);
                });

                MessageBox.Show("엑셀 다운로드가 완료되었습니다.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void StartLargeExcelBuildInBackground(
            string dbPath, string fromText, string toText,
            int? mode, double? heaterMin, double? heaterMax,
            double? airMin, double? airMax,
            int? motor, int? fanSpeed, double? motorCurrentMin,
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

            // ✅ 취소 버튼 표시
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
                    ExportAllToExcelOpenXml( dbPath, tmpPath, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin, progress, token);

                    token.ThrowIfCancellationRequested();

                    if (File.Exists(finalTempPath)) File.Delete(finalTempPath);
                    File.Move(tmpPath, finalTempPath);

                    return finalTempPath;
                }
                catch
                {
                    // ✅ 실패/취소 시 tmp 정리
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                    throw;
                }
            }, token)
            .ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    _excelBuilding = false;

                    // ✅ 취소 버튼 숨김
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

                        MessageBox.Show("엑셀 생성이 취소되었습니다.");
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
                        $"대용량 엑셀 생성이 완료되었습니다.\n\n'엑셀 저장'을 누르면 원하는 위치에 저장할 수 있습니다.\n(건수: {totalCount:N0})", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }


        private static string BuildWhere( int? mode, double? heaterMin, double? heaterMax, double? airMin, double? airMax, int? motor, int? fanSpeed, double? motorCurrentMin)
        {
            string where = "WHERE created_at >= @from AND created_at < @to";
            if (mode.HasValue) where += " AND mode = @mode";
            if (heaterMin.HasValue) where += " AND heater_temp >= @heaterMin";
            if (heaterMax.HasValue) where += " AND heater_temp <= @heaterMax";
            if (airMin.HasValue) where += " AND air_temp >= @airMin";
            if (airMax.HasValue) where += " AND air_temp <= @airMax";
            if (motor.HasValue) where += " AND motor = @motor";
            if (fanSpeed.HasValue) where += " AND fan_speed = @fanSpeed";
            if (motorCurrentMin.HasValue) where += " AND motor_current >= @motorCurrentMin";
            return where;
        }

        private static void ExportAllToExcelOpenXml( string dbPath, string xlsxPath, string fromText, string toText, int? mode, double? heaterMin, double? heaterMax, 
            double? airMin, double? airMax, int? motor, int? fanSpeed, double? motorCurrentMin, IProgress<(int percent, long done, long total)> progress, CancellationToken token)
        {
            string cs = $"Data Source={dbPath};";

            using (var con = new SqliteConnection(cs))
            {
                con.Open();

                string where = BuildWhere(mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);

                // total count(진행률 계산)
                long total;
                using (var cmdCount = con.CreateCommand())
                {
                    cmdCount.CommandText = "SELECT COUNT(1) FROM receive_data " + where + ";";
                    BindParams(cmdCount, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                    total = Convert.ToInt64(cmdCount.ExecuteScalar());
                }

                progress?.Report((0, 0, total));

                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT * FROM receive_data " +
                        where +
                        " ORDER BY datetime(created_at) DESC;";

                    BindParams(cmd, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);

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
                            // close previous sheet writer
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

                            // header row
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
                InlineString = new InlineString(new Text(text ?? ""))
            });
        }

        private void CancelBusy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _excelCts?.Cancel();
            }
            catch {  }
        }

        private void ResetExcelState(bool deleteTempFile)
        {
            // 생성 중이면 취소 시도 (안전장치)
            try { _excelCts?.Cancel(); } catch { }

            // temp 파일 정리
            if (deleteTempFile && !string.IsNullOrEmpty(_excelTempPath))
            {
                try
                {
                    if (File.Exists(_excelTempPath))
                        File.Delete(_excelTempPath);
                }
                catch { /* 파일 잠김/권한 이슈는 무시 */ }
            }

            _excelTempPath = null;

            if (btnExcel != null)
            {
                btnExcel.IsEnabled = true;
                btnExcel.Content = "엑셀";
                btnExcel.ToolTip = null;
            }
        }


    }
}
