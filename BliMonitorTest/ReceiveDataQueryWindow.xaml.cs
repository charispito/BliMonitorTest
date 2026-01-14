using BliMonitorTest.controls;
using BliMonitorTest.util.MonitoringDb;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
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

        private bool _isInitializing = true;

        public ReceiveDataQueryWindow()
        {
            // 로드 시점 이슈로 인한 디버깅용 플래그
            _isInitializing = true;
            InitializeComponent();
            _isInitializing = false;

            dpFrom.SelectedDate = DateTime.Today.AddDays(-1);
            dpTo.SelectedDate = DateTime.Today;

            Loaded += (s, e) => RefreshGrid();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _page = 1;
            RefreshGrid();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            // TextBox에서 Enter -> 조회
            e.Handled = true;
            Search_Click(sender, new RoutedEventArgs());
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;     // ✅ InitializeComponent 도중 발생 차단
            if (!IsLoaded) return;           // ✅ 화면 로드 전 차단(추가 안전)

            if (cbPageSize.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int size))
            {
                _pageSize = size;
                _page = 1;
                RefreshGrid();
            }
        }

        private void First_Click(object sender, RoutedEventArgs e) { _page = 1; RefreshGrid(); }

        private void Prev_Click(object sender, RoutedEventArgs e) { if (_page > 1) _page--; RefreshGrid(); }
        
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            int last = GetLastPage();
            if (_page < last) _page++;
            RefreshGrid();
        }

        private void Last_Click(object sender, RoutedEventArgs e) { _page = GetLastPage(); RefreshGrid(); }

        private int GetLastPage()
        {
            if (_pageSize <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(_totalCount / (double)_pageSize));
        }

        private void RefreshGrid()
        {
            if (!IsLoaded) return;  // ✅ Loaded 이전에는 아무 것도 하지 않음
            if (grid == null || txtPageInfo == null) return; // (원하면 throw 대신 return)

            DateTime from = (dpFrom.SelectedDate ?? DateTime.Today).Date;
            DateTime to = (dpTo.SelectedDate ?? DateTime.Today).Date;

            DateTime toExclusive = to.AddDays(1);

            if (toExclusive <= from)
            {
                MessageBox.Show("기간이 올바르지 않습니다.");
                return;
            }

            // ✅ 최대 1달 제한 (현재는 31일)
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

            try
            {
                // ✅ DB 경로: "실행 폴더 기준"
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Monitoring.db");
                if (!File.Exists(dbPath))
                {
                    MessageBox.Show($"DB 파일을 찾을 수 없습니다.\n{dbPath}");
                    return;
                }

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

                    // created_at TEXT 비교를 위해 ISO 문자열로 파라미터 전달
                    string fromText = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    string toText = toExclusive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                    // 1) total count
                    using (var cmdCount = con.CreateCommand())
                    {
                        cmdCount.CommandText = $"SELECT COUNT(1) FROM receive_data {where};";
                        BindParams(cmdCount, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                        _totalCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }

                    int lastPage = GetLastPage();
                    if (_page > lastPage) _page = lastPage;

                    int offset = (_page - 1) * _pageSize;

                    // 2) page rows
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText =
                            "SELECT * FROM receive_data " +
                            where +
                            " ORDER BY datetime(created_at) DESC " +
                            " LIMIT @limit OFFSET @offset;";

                        BindParams(cmd, fromText, toText, mode, heaterMin, heaterMax, airMin, airMax, motor, fanSpeed, motorCurrentMin);
                        cmd.Parameters.AddWithValue("@limit", _pageSize);
                        cmd.Parameters.AddWithValue("@offset", offset);

                        // SQL로그 출력 (디버깅용)
                        log.Debug(" FormatSqlLog : " + MonitoringDb.FormatSqlLog(cmd, "SQL"));

                        var dt = new DataTable();
                        using (var r = cmd.ExecuteReader())
                        {
                            dt.Load(r);
                        }

                        grid.ItemsSource = dt.DefaultView;
                        txtPageInfo.Text = $"총 {_totalCount:N0}건 / {_page:N0} / {lastPage:N0} 페이지";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "조회 오류", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ExcelDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 표시 중인 데이터를 DataView로 받음
                var view = grid.ItemsSource as DataView;
                if (view == null)
                {
                    MessageBox.Show("다운로드할 데이터가 없습니다.");
                    return;
                }

                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Excel 파일 (*.xlsx)|*.xlsx";
                sfd.FileName = "receive_data.xlsx";

                if (sfd.ShowDialog() != true)
                    return;

                // TODO: 여기서 view.Table(DataTable)을 Excel로 저장
                // 예: ClosedXML(권장) 또는 EPPlus 등 사용

                MessageBox.Show("엑셀 다운로드 기능은 저장 로직 연결이 필요합니다.\n(현재는 파일 경로 선택까지 동작)", "안내");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "엑셀 다운로드 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
