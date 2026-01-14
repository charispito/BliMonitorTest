using BliMonitorTest.data;
using BliMonitorTest.util;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Path = System.IO.Path;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// OneChannelValueDetail.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OneChannelValueDetail : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(OneChannelValueDetail));

        // Database 관련 변수
        private SqliteConnection _db;
        private SqliteTransaction _tx;
        private string _dbPath;
        private string _csvPath;
        private bool _csvHeaderWritten;
        private bool _dbReady;

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
                    Statebox.cont.Content = "운전중";
                    Statebox.cont.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                    this.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                }
                else
                {
                    Statebox.cont.Content = "대기중";
                    Statebox.cont.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    this.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }
        }
        public int number = 1;
        public bool IsNewVersion = false;
        public float off_sum = 0;
        public int air_sum = 0;
        public SerialPort port;
        public OxyView chartView;
        //public WPFChartView chartView;
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
            //StartButton.Click += StartButton_Click;
            //StopButton.Click += StopButton_Click;
            ParameterButton.Click += ParameterButton_Click;
            ApplyNewVersion.Click += ApplyNewVersion_Click;
            list1.Add(new KeyValuePair<double, int>(0, 0));
            list2.Add(new KeyValuePair<double, int>(0, 0));
            list3.Add(new KeyValuePair<double, int>(0, 0));
            list4.Add(new KeyValuePair<double, int>(0, 0));
            list5.Add(new KeyValuePair<double, int>(0, 0));
            list6.Add(new KeyValuePair<double, int>(0, 0));
            list7.Add(new KeyValuePair<double, int>(0, 0));
            list8.Add(new KeyValuePair<double, double>(0, 0));
            Item1.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item1Check, new object[0]);
            };
            Item2.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item2Check, new object[0]);
            };
            Item3.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item3Check, new object[0]);
            };
            Item4.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item4Check, new object[0]);
            };
            Item11.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item5Check, new object[0]);
            };
            Item12.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item6Check, new object[0]);
            };
            Item13.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item7Check, new object[0]);
            };
            Item14.MouseLeftButtonDown += (s, e) => {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item8Check, new object[0]);
            };
            Loaded += OneChannelValueDetail_Loaded;
        }

        private void OneChannelValueDetail_Loaded(object sender, RoutedEventArgs e)
        {
            //1 2 6 3 8 5 7 4
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item1Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item2Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item6Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item3Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item8Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item5Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item7Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item4Check, new object[0]);
        }

        private void ApplyNewVersion_Click(object sender, RoutedEventArgs e)
        {
            IsNewVersion = (sender as CheckBox).IsChecked.Value;
            OnCheckChanged();
        }

        private void ParameterButton_Click(object sender, RoutedEventArgs e)
        {
            if (port != null && port.IsOpen)
            {
                parameterWindow = new ParameterWindow(port, this);
                parameterWindow.Show();
            }

        }

        private void initFile()
        {
            air_sum = 0;
            off_sum = 0;
            number = 0;
            if (IsNewVersion)
            {
                streamWriter.WriteLine("날짜,모드,남은 시간,히터 온도,히터 오프타임,배기온도,FAN Speed," +
                "평균히터오프타임,열풍온타임,MOTOR,모터 전류,번호,오프타임합,오프평균,배기 합,배기 평균");
            }
            else
            {
                streamWriter.WriteLine("날짜,모드,남은 시간,히터 온도,히터 오프타임,배기온도,FAN Speed," +
                "열풍온도,열풍온타임,MOTOR,모터 전류,번호,오프타임합,오프평균,배기 합,배기 평균");
            }
        }

        /*
        public void WriteFile(ReadData data)
        {
            number++;
            air_sum += data.air_temp;
            off_sum += data.heater_off_time;

            if (streamWriter != null)
            {
                if (Modify)
                {
                    streamWriter.Close();
                    initPath();
                    Modify = false;
                }else if(streamWriter.BaseStream == null)
                {
                    initPath();
                }
                else
                {
                    streamWriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}", data.date, data.mode, data.remain_time, data.heater_temp, data.heater_off_time,
                    data.air_temp, data.fan_speed, data.hot_air_temp, data.hot_air_ontime, data.motor, data.motor_current, number, off_sum, (double)(off_sum / (double)number), air_sum, (double)(air_sum / (double)number));
                }                
            }
            else
            {
                initPath();
                Modify=false;
            }

        }
        */

        public void WriteFile(ReadData data)
        {
            number++;
            air_sum += data.air_temp;
            off_sum += data.heater_off_time;

            if (streamWriter != null)
            {
                if (Modify)
                {
                    streamWriter.Close();
                    initPath();
                    Modify = false;
                }
                else if (streamWriter.BaseStream == null)
                {
                    initPath();
                }
                else
                {
                    // ✅ 데이터 검증
                    if (!ValidateData(data, out var reason))
                    {
                        log.Warn("Skip write: " + reason);
                        return;
                    }

                    // 1) CSV 기록 (기존 라인 그대로)
                    streamWriter.WriteLine(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                        data.date, data.mode, data.remain_time, data.heater_temp, data.heater_off_time,
                        data.air_temp, data.fan_speed, data.hot_air_temp, data.hot_air_ontime,
                        data.motor, data.motor_current, number, off_sum, (double)(off_sum / (double)number),
                        air_sum, (double)(air_sum / (double)number)
                    );
                    streamWriter.Flush();

                    // 2) SQLite 기록
                    MonitoringDb.InsertDb( ref _db, _dbPath, ref _dbReady, IsNewVersion, data, number, off_sum, air_sum );
                }
            }
            else
            {
                initPath();
                Modify = false;
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

            streamWriter = new StreamWriter(_csvPath, append: true, Encoding.Default);

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

            // 2) DB 준비
            MonitoringDb.EnsureDb(ref _db, _dbPath, ref _dbReady);
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

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!port.IsOpen)
            {
                MessageBox.Show("연결 되지 않았습니다.");
                return;
            }
            if (run)
            {
                MessageBox.Show("이미 운전중입니다.");
                return;
            }
            OnTestStart();

            if (IsNewVersion)
            {
                byte[] command = Protocol.GetNewCommand(2);
                port.Write(command, 0, command.Length);
            }
            else
            {
                byte[] command = Protocol.GetCommand(2);
                port.Write(command, 0, command.Length);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!port.IsOpen)
            {
                MessageBox.Show("연결 되지 않았습니다.");
                return;
            }
            if (streamWriter != null)
            streamWriter.Close();
            streamWriter = null;
            //chartView.ItemRun[0] = false;
            if (IsNewVersion)
            {
                byte[] command = Protocol.GetNewCommand(3);
                port.Write(command, 0, command.Length);
                log.Debug("============        LOG DATA START  396 LINE     ==================");
                ByteLogHelper.LogPacket(command, "TX");
                ByteLogHelper.ToHexWith0x(command);
                ByteLogHelper.DumpLinesWith0x(command, 16);
                log.Debug("============        LOG DATA END       ==================");
            }
            else
            {
                byte[] command = Protocol.GetCommand(3);
                port.Write(command, 0, command.Length);
                log.Debug("============        LOG DATA START  405 LINE       ==================");
                ByteLogHelper.LogPacket(command, "TX");
                ByteLogHelper.ToHexWith0x(command);
                ByteLogHelper.DumpLinesWith0x(command, 16);
                log.Debug("============        LOG DATA END       ==================");
            }
        }
    }
}
