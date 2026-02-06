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
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item3Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item4Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item5Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item6Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item7Check, new object[0]);
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Item8Check, new object[0]);            
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
            streamWriter.WriteLine(
                "날짜, 채널, 소스, ModelNo, SW, HeaterB, ColdB, LowWater, Floor, UVByte," +
                "Sol3_1, Sol3_2, Sol3_3, AirVent, CV, Buttons, Pump, ColdSol, NormalSol, HotSol1," +
                "Needle, PEL_B, Cmd, Payload, Checksum, End"
            );
        }

        public void WriteFile(BliResponse57Packet resp, int channelNo, int sourceType)
        {
            if (resp == null) return;

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

                string dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string rawHex = BitConverter.ToString(resp.Raw ?? Array.Empty<byte>()).Replace("-", "");

                // CSV 기록(TO-BE)
                streamWriter.WriteLine(string.Join(",",
                    dateStr, channelNo, sourceType,
                    resp.ModelNo, resp.SwVer, resp.HeaterTempB, resp.ColdTempB,
                    resp.LowWater, resp.FloorSensor, resp.UvLedByte,
                    resp.Sol3Way1, resp.Sol3Way2, resp.Sol3Way3,
                    resp.AirVentSol, resp.CvSol, resp.ButtonFlags,
                    resp.Pump, resp.ColdSol, resp.NormalSol, resp.HotSol1,
                    resp.NeedlePos, resp.PelVoltageB,
                    resp.CmdByte, resp.PayloadSize, resp.Checksum, resp.EndPacket
                ));
                streamWriter.Flush();

                // 2) SQLite 기록 : Queue 방식으로 비동기 처리
                BliMonitorTest.util.MonitoringDb.MonitoringDbWriteService.Instance.Start(BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());
                BliMonitorTest.util.MonitoringDb.MonitoringDbWriteService.Instance.Enqueue(resp, channelNo, sourceType);
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
