using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using BliMonitorTest.data;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
using log4net;
using Microsoft.Data.Sqlite;
using Path = System.IO.Path;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// OneChannelValueDetail.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OneChannelValueDetail : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(OneChannelValueDetail));

        // Database 관련 변수
        private string _dbPath;
        private string _csvPath;
        private bool _csvHeaderWritten;

        private int _ConnectState = 0;
        private bool _ParameterMode = false;

        public bool ParameterMode {
            get
            {
                return _ParameterMode;
            }
            set
            {
                _ParameterMode = value;

                if (_ParameterMode)
                {
                    OnParameterLoadAction();
                }
            } 
        }
        private bool _SaveInDesktop = false;
        public bool SaveInDesktop
        {
            get{ 
                return _SaveInDesktop;
            }
            set {
                _SaveInDesktop = value;
                Modify = true;
            }
        }
        public ParameterWindow parameterWindow;
        public delegate void OnStart();
        public event OnStart OnTestStart;
        public delegate void OnParamerLoad();
        public event OnParamerLoad OnParameterLoadAction;
        public delegate void CheckChanged();
        public event CheckChanged OnCheckChanged;
        public string FName { get; set; }
        public bool Modify = false;
        public bool _run = false;

        public bool run {
            get { return _run; }
            set { 
                _run = value;
                if (value)
                {
                    Statebox.cont.Text = "운전중";
                    Statebox.cont.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                    this.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                }
                else
                {
                    Statebox.cont.Text = "대기중";
                    Statebox.cont.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    this.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }
        }

        public int number = 1;
        public float off_sum = 0;
        public int air_sum = 0;
        public SerialPort port;
        public OxyView chartView;
        public StreamWriter streamWriter;
        public IList<KeyValuePair<double, int>> list1 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list2 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list3 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list4 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list5 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list6 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, int>> list7 = new ObservableCollection<KeyValuePair<double, int>>();
        public IList<KeyValuePair<double, double>> list8 = new ObservableCollection<KeyValuePair<double, double>>();
        public int ConnectState
        {
            get { return _ConnectState; }
            set { 
                _ConnectState = value;
                if(value == 0)
                {
                    ChannelView.Text = "연결안됨";
                    ChannelView.cont.Foreground = Brushes.Red;
                }
                else
                {
                    ChannelView.Text = "연결됨";
                    ChannelView.cont.Foreground = Brushes.Blue;
                }
            }
        }
        public void setParameter()
        {
            OnParameterLoadAction();
        }

        public OneChannelValueDetail()
        {
            InitializeComponent();
            ParameterButton.Click += ParameterButton_Click;
            list1.Add(new KeyValuePair<double, int>(0, 0));
            list2.Add(new KeyValuePair<double, int>(0, 0));
            list3.Add(new KeyValuePair<double, int>(0, 0));
            list4.Add(new KeyValuePair<double, int>(0, 0));
            list5.Add(new KeyValuePair<double, int>(0, 0));
            list6.Add(new KeyValuePair<double, int>(0, 0));
            list7.Add(new KeyValuePair<double, int>(0, 0));
            list8.Add(new KeyValuePair<double, double>(0, 0));
            Item1.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item1Check, new object[0]);
            };

            Item2.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item2Check, new object[0]);
            };

            Item3.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item3Check, new object[0]);
            };

            Item5.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item4Check, new object[0]);
            };

            Item20.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item5Check, new object[0]);
            };

            Item21.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item6Check, new object[0]);
            };

            Item22.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item7Check, new object[0]);
            };

            Item24.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Item8Check, new object[0]);
            };

            Loaded += OneChannelValueDetail_Loaded;
        }

        private void OneChannelValueDetail_Loaded(object sender, RoutedEventArgs e)
        {
            //1 2 6 3 8 5 7 4
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item1Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item2Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item3Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item4Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item5Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item6Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item7Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item8Check, new object[0]);            
        }

        private bool CanOpenParameterWindow()
        {
            OneChannelWindow parent = Window.GetWindow(this) as OneChannelWindow;
            if (parent == null)
                return false;

            return parent.CanOpenParameterPopup();
        }

        private void ParameterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OneChannelWindow parent = Window.GetWindow(this) as OneChannelWindow;
                bool isDummyMode = parent != null && parent.IsDummyMode;

                if (!CanOpenParameterWindow())
                {
                    ToastMessage.ToastService.AppToast.Show("패킷 수신 상태가 아니어서 파라미터 창을 열 수 없습니다.");
                    return;
                }

                if (parameterWindow != null)
                {
                    if (parameterWindow.IsVisible)
                    {
                        if (parameterWindow.WindowState == WindowState.Minimized)
                            parameterWindow.WindowState = WindowState.Normal;

                        parameterWindow.Activate();
                        parameterWindow.Topmost = true;
                        parameterWindow.Topmost = false;
                        parameterWindow.Focus();
                        return;
                    }

                    parameterWindow = null;
                }

                if (isDummyMode)
                {
                    parameterWindow = new ParameterWindow(null, this);
                }
                else
                {
                    parameterWindow = new ParameterWindow(port, this);
                }

                parameterWindow.Closed += (s, args) => { parameterWindow = null; };
                parameterWindow.Show();
            }
            catch (Exception ex)
            {
                log.Error("ParameterButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("파라미터 창을 여는 중 오류가 발생했습니다.");
            }
        }

        private void initFile()
        {
            var headerTable = BuildReceiveExportTableForSinglePacketHeader();
            if (headerTable.Columns.Count == 0)
                return;

            var headers = new List<string>();
            foreach (DataColumn col in headerTable.Columns)
                headers.Add(EscapeCsv(col.ColumnName));

            streamWriter.WriteLine(string.Join(",", headers));
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }

        private DataTable BuildSingleReceiveRowTable(Duo8StatusPacket resp, int channelNo, int sourceType)
        {
            var dt = new DataTable();

            dt.Columns.Add("created_at", typeof(string));
            dt.Columns.Add("source_type", typeof(int));
            dt.Columns.Add("channel_no", typeof(int));
            dt.Columns.Add("model_code", typeof(int));
            dt.Columns.Add("error_code", typeof(int));
            dt.Columns.Add("water_init_done", typeof(int));
            dt.Columns.Add("water_init_go", typeof(int));
            dt.Columns.Add("empty_detect", typeof(int));
            dt.Columns.Add("buffer_low", typeof(int));
            dt.Columns.Add("pcb_hw_version", typeof(int));
            dt.Columns.Add("pcb_sw_version", typeof(int));
            dt.Columns.Add("heater_pwm", typeof(int));
            dt.Columns.Add("night", typeof(int));
            dt.Columns.Add("test_mode", typeof(int));
            dt.Columns.Add("mode_selected", typeof(int));
            dt.Columns.Add("qty_selected", typeof(int));
            dt.Columns.Add("dispense_phase", typeof(int));
            dt.Columns.Add("dispense_sub_phase", typeof(int));
            dt.Columns.Add("hot_temp_raw", typeof(int));
            dt.Columns.Add("cold_temp_raw", typeof(int));
            dt.Columns.Add("float_low_stable", typeof(int));
            dt.Columns.Add("ball_top_full_stable", typeof(int));
            dt.Columns.Add("water_buf_full_stable", typeof(int));
            dt.Columns.Add("heater_output", typeof(int));
            dt.Columns.Add("compressor_output", typeof(int));
            dt.Columns.Add("hot_valve_output", typeof(int));
            dt.Columns.Add("cold_select_output", typeof(int));
            dt.Columns.Add("outlet_valve_output", typeof(int));
            dt.Columns.Add("button_info", typeof(int));
            dt.Columns.Add("status_a", typeof(int));
            dt.Columns.Add("status_b", typeof(int));

            var row = dt.NewRow();
            row["created_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            row["source_type"] = sourceType;
            row["channel_no"] = channelNo;
            row["model_code"] = resp.ModelCode;
            row["error_code"] = resp.ErrorCode;
            row["water_init_done"] = resp.WaterInitDone;
            row["water_init_go"] = resp.WaterInitGo;
            row["empty_detect"] = resp.EmptyDetect;
            row["buffer_low"] = resp.BufferLow;
            row["pcb_hw_version"] = resp.PcbHwVersion;
            row["pcb_sw_version"] = resp.PcbSwVersion;
            row["heater_pwm"] = resp.HeaterPwm;
            row["night"] = resp.Night;
            row["test_mode"] = resp.TestMode;
            row["mode_selected"] = resp.ModeSelected;
            row["qty_selected"] = resp.QtySelected;
            row["dispense_phase"] = resp.DispensePhase;
            row["dispense_sub_phase"] = resp.DispenseSubPhase;
            row["hot_temp_raw"] = resp.HotTempRaw;
            row["cold_temp_raw"] = resp.ColdTempRaw;
            row["float_low_stable"] = resp.FloatLowStable;
            row["ball_top_full_stable"] = resp.BallTopFullStable;
            row["water_buf_full_stable"] = resp.WaterBufFullStable;
            row["heater_output"] = resp.HeaterOutput;
            row["compressor_output"] = resp.CompressorOutput;
            row["hot_valve_output"] = resp.HotValveOutput;
            row["cold_select_output"] = resp.ColdSelectOutput;
            row["outlet_valve_output"] = resp.OutletValveOutput;
            row["button_info"] = resp.ButtonInfo;
            row["status_a"] = resp.StatusA;
            row["status_b"] = resp.StatusB;

            dt.Rows.Add(row);
            return dt;
        }

        private DataTable BuildReceiveExportTableForSinglePacketHeader()
        {
            var dt = new DataTable();
            dt.Columns.Add("created_at", typeof(string));
            dt.Columns.Add("source_type", typeof(int));
            dt.Columns.Add("channel_no", typeof(int));
            dt.Columns.Add("model_code", typeof(int));
            dt.Columns.Add("error_code", typeof(int));
            dt.Columns.Add("water_init_done", typeof(int));
            dt.Columns.Add("water_init_go", typeof(int));
            dt.Columns.Add("empty_detect", typeof(int));
            dt.Columns.Add("buffer_low", typeof(int));
            dt.Columns.Add("pcb_hw_version", typeof(int));
            dt.Columns.Add("pcb_sw_version", typeof(int));
            dt.Columns.Add("heater_pwm", typeof(int));
            dt.Columns.Add("night", typeof(int));
            dt.Columns.Add("test_mode", typeof(int));
            dt.Columns.Add("mode_selected", typeof(int));
            dt.Columns.Add("qty_selected", typeof(int));
            dt.Columns.Add("dispense_phase", typeof(int));
            dt.Columns.Add("dispense_sub_phase", typeof(int));
            dt.Columns.Add("hot_temp_raw", typeof(int));
            dt.Columns.Add("cold_temp_raw", typeof(int));
            dt.Columns.Add("float_low_stable", typeof(int));
            dt.Columns.Add("ball_top_full_stable", typeof(int));
            dt.Columns.Add("water_buf_full_stable", typeof(int));
            dt.Columns.Add("heater_output", typeof(int));
            dt.Columns.Add("compressor_output", typeof(int));
            dt.Columns.Add("hot_valve_output", typeof(int));
            dt.Columns.Add("cold_select_output", typeof(int));
            dt.Columns.Add("outlet_valve_output", typeof(int));
            dt.Columns.Add("button_info", typeof(int));
            dt.Columns.Add("status_a", typeof(int));
            dt.Columns.Add("status_b", typeof(int));

            ReceiveDataExportFormatter.AddReceiveInterpretColumns(dt);
            return ReceiveDataExportFormatter.BuildReceiveExportTable(dt);
        }

        private void EnsureWriterReady()
        {
            if (streamWriter != null)
            {
                if (Modify)
                {
                    try { streamWriter.Close(); } catch { }
                    initPath();
                    Modify = false;
                }
                else if (streamWriter.BaseStream == null)
                {
                    initPath();
                }
            }
            else
            {
                initPath();
                Modify = false;
            }
        }

        private void WriteCsvLine(string line)
        {
            if (streamWriter == null || string.IsNullOrWhiteSpace(line))
                return;

            streamWriter.WriteLine(line);
            streamWriter.Flush();
        }

        private void EnsureDbServiceStarted()
        {
            try
            {
                MonitoringDbWriteService.Instance.Start(StoragePathUtil.GetDbPath());
            }
            catch (Exception ex)
            {
                log.Warn("MonitoringDbWriteService Start 실패", ex);
            }
        }

        public void WriteFile(Duo8StatusPacket resp, int channelNo, int sourceType)
        {
            if (resp == null) return;

            try
            {
                EnsureWriterReady();

                if (streamWriter == null)
                    return;

                var dt = BuildSingleReceiveRowTable(resp, channelNo, sourceType);
                ReceiveDataExportFormatter.AddReceiveInterpretColumns(dt);
                var exportTable = ReceiveDataExportFormatter.BuildReceiveExportTable(dt);

                if (exportTable.Rows.Count > 0)
                {
                    var row = exportTable.Rows[0];
                    var values = new List<string>();

                    foreach (DataColumn col in exportTable.Columns)
                        values.Add(EscapeCsv(Convert.ToString(row[col])));

                    WriteCsvLine(string.Join(",", values));
                }

                try
                {
                    EnsureDbServiceStarted();
                    MonitoringDbWriteService.Instance.Enqueue(resp, channelNo, sourceType);
                }
                catch (Exception ex)
                {
                    log.Warn("Duo8 상태패킷 DB 저장 실패", ex);
                }
            }
            catch (Exception ex)
            {
                log.Warn("WriteFile(Duo8StatusPacket) 실패", ex);
            }
        }

        private void initPath()
        {
            string dir = @".\ChannelData";
            string dbDir = AppDomain.CurrentDomain.BaseDirectory;
            
            if (_SaveInDesktop)
                dir = System.IO.Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "ChannelData" );

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string baseName = FName + "_ch1";

            _csvPath = System.IO.Path.Combine(dir, baseName + ".csv");
            _dbPath = StoragePathUtil.GetDbPath();

            // 1) CSV 열기
            bool fileExists = File.Exists(_csvPath);
            long fileLen = fileExists ? new FileInfo(_csvPath).Length : 0;

            streamWriter = new StreamWriter(_csvPath, append: true, new UTF8Encoding(true));

            // 헤더는 비어있는 파일일 때만
            if (fileLen == 0)
            {
                initFile();
                _csvHeaderWritten = true;
                streamWriter.Flush();
            }
            else
            {
                // "이미 있다"의 의미로 true
                _csvHeaderWritten = true;
            }
        }

        private bool ValidateData(ReadData data, out string reason)
        {
            reason = null;

            if (data == null) { reason = "data is null"; return false; }
            if (string.IsNullOrWhiteSpace(data.date)) { reason = "date is empty"; return false; }

            if (double.IsNaN(data.heater_temp) || double.IsInfinity(data.heater_temp))
            { reason = "heater_temp NaN/Inf"; return false; }

            return true;
        }

        public void setHandler(RoutedEventHandler handler)
        {
            Item1Check.Click += handler;
            Item2Check.Click += handler;
            Item3Check.Click += handler;
            Item4Check.Click += handler;
            Item5Check.Click += handler;
            Item6Check.Click += handler;
            Item7Check.Click += handler;
            Item8Check.Click += handler;
        }

    }
}
