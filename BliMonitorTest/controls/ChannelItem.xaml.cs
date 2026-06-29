using BliMonitorTest.data;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
using DocumentFormat.OpenXml.InkML;
using log4net;
using Microsoft.Data.Sqlite;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BliMonitorTest.controls
{
    public partial class ChannelItem : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ChannelItem));
        private DateTime? _dummyStartTime;

        public StreamWriter streamWriter;
        private bool _ParameterMode = false;

        // Database 관련 변수
        private string _dbPath;
        private string _csvPath;
        private bool _csvHeaderWritten;

        public bool ParameterMode
        {
            get
            {
                return _ParameterMode;
            }
            set
            {
                _ParameterMode = value;
                if (_ParameterMode)
                {
                    Console.WriteLine("load");
                    OnParameterLoadAction?.Invoke(0);
                }
            }
        }

        public bool IsDummyEnabled
        {
            get => DummyCheck?.IsChecked == true;
            set
            {
                if (DummyCheck != null) DummyCheck.IsChecked = value;
            }
        }

        string now = DateTime.Now.ToString("yyyy-MM-dd");
        public bool Response = false;
        public int number = 1;
        public delegate void CheckChanged();
        public event CheckChanged OnCheckChanged;
        public delegate void OnParamerLoad(int type);
        public event OnParamerLoad OnParameterLoadAction;
        //public bool IsNewVersion = false;
        private bool _SaveInDesktop = false;
        public bool SaveInDesktop
        {
            get
            {
                return _SaveInDesktop;
            }
            set
            {
                _SaveInDesktop = value;
                Modify = true;
            }
        }
        public string FName { get; set; }
        public bool Modify = false;
        public float off_sum = 0;
        public int air_sum = 0;
        public TimeSpan TestTime;

        public int NonResponse { get; set; } = 0;
        public Dictionary<int, int> seriesList { get; set; }
        public MultiParameterWindow ParameterWindow { get; set; }
        private bool _run = false;
        public bool run
        {
            get
            {
                return _run;
            }
            set
            {
                _run = value;
                if (value)
                {
                    StateBox.cont.Text = "운전중";
                    this.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                    StateBox.cont.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                }
                else
                {
                    StateBox.cont.Text = "정지";
                    this.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    StateBox.cont.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }
        }

        public int ItemIndex = 0;
        public int ParameterCount = 0;
        public OxyView chartView { get; set; }
        public TcpClient client { get; set; }
        public long TimeMills { get; set; }
        private int _Channel;

        public void clearData()
        {
            NonResponse = 0;
            run = false;
            //ApplyNewVersion.IsChecked = false;
            Item1.cont.Text = "";
            Item2.cont.Text = "";
            Item3.cont.Text = "";
            Item4.cont.Text = "";
            Item5.cont.Text = "";
            Item6.cont.Text = "";
            Item11.cont.Text = "";
            Item12.cont.Text = "";
            Item13.cont.Text = "";
            Item14.cont.Text = "";
            Item15.cont.Text = "";
            Item16.cont.Text = "";
            Item17.cont.Text = "";
            Item18.cont.Text = "";
            StateBox.cont.Text = "";
            TestTime = TimeSpan.Zero;
        }

        public int Channel
        {
            get { return _Channel; }
            set
            {
                _Channel = value;
                Dispatcher.Invoke(new Action(() => { ChannelView.cont.Text = value.ToString(); }));
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
                //BliMonitorTest.util.MonitoringDb.MonitoringDbWriteService.Instance.Start(BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());
                //BliMonitorTest.util.MonitoringDb.MonitoringDbWriteService.Instance.Enqueue(resp, channelNo, sourceType);
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
                dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChannelData");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string baseName = FName + $"_ch{_Channel}" + ".csv";

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

        public ChannelItem()
        {
            InitializeComponent();
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

            ParameterButton.Click += ParameterButton_Click;
        }

        private void ParameterButton_Click(object sender, RoutedEventArgs e)
        {
            // 1) 더미가 아니면 연결 체크
            if (!IsDummyEnabled)
            {
                if (client == null || !client.Connected)
                {
                    ToastMessage.ToastService.AppToast.Show("연결 되지 않았습니다.");
                    return;
                }
            }
            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    ParameterWindow = new MultiParameterWindow(this, this._Channel);
                    ParameterWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    ParameterWindow.Show();

                    if (IsDummyEnabled)
                        return;
                }));
            });
        }

        public void CloseWriter()
        {
            if (streamWriter != null)
            {
                streamWriter.Close();
            }
        }

        public void setParameter(int type)
        {
            OnParameterLoadAction(1);
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

        public void SetView(byte[] data)
        {
            if (data.Length < 57)
                return;
            if (data[3] != 57)
                return;

            if (data[0] != 0x12)
                return;
            if (data.Last() != 0x34)
                return;

            bool isDummy = IsDummyEnabled;

            data.PrintHex(1);

            long total_second = (long)TestTime.TotalSeconds;
            double total_minute = total_second / 60;
            double remain_second = (double)total_second % 60 / 100.0;
            double remain_value = 0.40 / 60;

            if (total_second > 0)
            {
                total_minute += (remain_second + remain_value * (total_second % 60));
            }
            else
            {
                total_minute += remain_second;
            }

            // 더미일경우
            if (isDummy)
            {
                // null일 때만 1회 설정 (핵심)
                if (_dummyStartTime == null)
                    _dummyStartTime = DateTime.Now;

                TimeSpan elapsed = DateTime.Now - _dummyStartTime.Value;

                total_second = (long)elapsed.TotalSeconds;
                total_minute = elapsed.TotalMinutes;   // 분 단위 double (가장 깔끔)
            }
            else
            {
                // 더미 해제 시 리셋(선택)
                _dummyStartTime = null;

                // 다시 TestTime 기반으로 (원래 의미 유지)
                total_second = (long)TestTime.TotalSeconds;
                total_minute = TestTime.TotalMinutes;
            }

            // 데이터 화면 매핑
            byte modelCode = data[5];
            byte swVersion = data[6];

            int heaterTemp = data[7];
            int coldTemp = data[8];

            bool waterLevelLow = data[9] != 0;   // ON/OFF
            bool floorSensor = data[10] != 0;  // ON/OFF

            bool uvLed = data[11] != 0;

            // 3방 SOL: data[12] 비트필드 (바이트의 0번째 비트부터)
            byte triSolByte = data[12];
            bool triSol1 = (triSolByte & (1 << 0)) != 0; // 0번째 비트 → 3방 SOL 1
            bool triSol2 = (triSolByte & (1 << 1)) != 0; // 1번째 비트 → 3방 SOL 2
            bool triSol3 = (triSolByte & (1 << 2)) != 0; // 2번째 비트 → 3방 SOL 3

            bool airVentSol = data[13] != 0;
            bool cvSol = data[14] != 0;

            // 버튼 2바이트 (LSB→MSB, bit0부터)
            ushort buttons = (ushort)(data[15] | (data[16] << 8));
            bool btnCont = (buttons & (1 << 0)) != 0;
            bool btnVolume = (buttons & (1 << 1)) != 0;
            bool btnFree = (buttons & (1 << 2)) != 0;
            bool btnHighHot = (buttons & (1 << 3)) != 0;
            bool btnHot = (buttons & (1 << 4)) != 0;
            bool btnWarm = (buttons & (1 << 5)) != 0;
            bool btnChild = (buttons & (1 << 6)) != 0;
            bool btnRoom = (buttons & (1 << 7)) != 0;
            bool btnMildCold = (buttons & (1 << 8)) != 0;
            bool btnCold = (buttons & (1 << 9)) != 0;

            bool pumpOn = data[17] != 0;
            bool coldSol = data[18] != 0;
            bool normalSol = data[19] != 0;
            bool hotSol1 = data[20] != 0;

            byte needleState = data[21];
            byte compVolt = data[22];

            // 4) 중앙 좌측 UI 바인딩
            Item1.cont.Text = $"{heaterTemp}ºC";                 // 히터 온도
            Item2.cont.Text = $"{coldTemp}ºC";                   // 냉수 온도
            Item3.cont.Text = waterLevelLow ? "ON" : "OFF";      // 수위센서
            Item4.cont.Text = floorSensor ? "ON" : "OFF";        // 플로어 센서
            Item5.cont.Text = uvLed ? "ON" : "OFF";              // UV LED

            // 3방 SOL 요약(0번째 비트부터: 1,2,3)
            Item6.cont.Text = $"{OnOff(triSol1)} / {OnOff(triSol2)} / {OnOff(triSol3)}";

            Item11.cont.Text = airVentSol ? "ON" : "OFF";        // Air Vent Sol
            Item12.cont.Text = cvSol ? "ON" : "OFF";             // C/V
            Item13.cont.Text = pumpOn ? "ON" : "OFF";            // PUMP
            Item14.cont.Text = coldSol ? "ON" : "OFF";           // Cold Sol
            Item15.cont.Text = normalSol ? "ON" : "OFF";         // Normal Sol
            Item16.cont.Text = hotSol1 ? "ON" : "OFF";           // Hot Sol

            Item17.cont.Text = $"{NeedleToText(needleState)}";
            Item18.cont.Text = getModelName(modelCode);

            // 5-1) 수신 데이터 파일 기록
            var resp = ResponsePacket57.Parse(data);
            if (resp != null)
            {
                WriteFile(resp, _Channel, sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE);
            }

            if (Item1Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10], new DataPoint(total_minute, heaterTemp));
            }
            if (Item2Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 1], new DataPoint(total_minute, coldTemp));
            }
            if (Item3Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 2], new DataPoint(total_minute, waterLevelLow ? 1 : 0));
            }
            if (Item4Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 3], new DataPoint(total_minute, floorSensor ? 1 : 0));
            }
            if (Item5Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 4], new DataPoint(total_minute, airVentSol ? 1 : 0));
            }
            if (Item6Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 5], new DataPoint(total_minute, cvSol ? 1 : 0));
            }
            if (Item7Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 6], new DataPoint(total_minute, pumpOn ? 1 : 0));
            }
            if (Item8Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 7], new DataPoint(total_minute, coldSol ? 1 : 0));
            }

            lock (chartView.ViewModel)
            {
                //log.Debug($"isDummy={IsDummyEnabled} total_second={total_second} total_minute={total_minute:F3} start={_dummyStartTime:HH:mm:ss.fff}");
                chartView.ViewModel.panXAxis(total_minute);
            }
        }

        private string OnOff(bool v) => v ? "ON" : "OFF";

        private string NeedleToText(byte st)
        {
            switch (st)
            {
                case 0: return "상승상태";
                case 1: return "동작중";
                case 2: return "하강상태";
                default: return st.ToString();
            }
        }

        private string getModelName(int model)
        {
            switch (model)
            {
                case 0:
                    return "BSH-311";
                case 1:
                    return "BSS-311";
                case 2:
                    return "BSS-314";
                case 3:
                    return "BSS-310";
                case 4:
                    return "BSS-330";
                case 5:
                    return "BSS-341";
                case 6:
                    return "DUO 8";
                case 7:
                    return "Hybrid";
                default:
                    return "";
            }
        }

        private int getMotorValue(int run)
        {
            switch (run)
            {
                case 2:
                    return 75;
                case 3:
                    return 75;
                case 5:
                    return 25;
                case 8:
                    return 50;
                case 9:
                    return 50;
                default:
                    return 0;
            }
        }

        private string getMotorState(int run)
        {
            switch (run)
            {
                case 2:
                    return "CW";
                case 3:
                    return "CW";
                case 4:
                    return "CCW";
                case 5:
                    return "CCW";
                case 8:
                    return "STOP";
                case 9:
                    return "STOP";
                default:
                    return "";
            }
        }

        private string GetErrorName(int[] errors0, int[] errors1)
        {
            Array.Reverse(errors0);
            Array.Reverse(errors1);
            StringBuilder builder = new StringBuilder();
            int cnt = 0;
            if (errors0 != null && errors0.Length > 0)
                for (int i = 0; i < errors0.Length; i++)
                {
                    if (errors0[i] == 1)
                    {
                        cnt++;
                        switch (i)
                        {
                            case 0:
                                builder.AppendLine("모터 과부하", true);
                                break;
                            case 1:
                                builder.AppendLine("모터 단선", true);
                                break;
                            case 2:
                                builder.AppendLine("히터 동작 이상", true);
                                break;
                            case 3:
                                if (errors0[2] != 1)
                                    builder.AppendLine("히터 동작 이상", true);
                                else
                                    cnt--;
                                break;
                            case 4:
                                builder.AppendLine("히터 센서 이상", true);
                                break;
                            case 5:
                                builder.AppendLine("배기 온도 이상", true);
                                break;
                            case 6:
                                builder.AppendLine("배기 센서 이상", true);
                                break;
                            case 7:
                                builder.AppendLine("배기 팬 이상", true);
                                break;
                        }
                    }
                }
            if (errors1 != null && errors1.Length > 0)
                for (int i = 0; i < errors1.Length; i++)
                {
                    if (errors1[i] == 1)
                    {
                        cnt++;
                        switch (i)
                        {
                            case 0:
                                builder.AppendLine("이물질감지", true);
                                break;
                            case 1:
                                builder.AppendLine("도어 열림", true);
                                break;
                            case 2:
                                if (errors1[1] != 1)
                                    builder.AppendLine("도어 열림", true);
                                else
                                    cnt--;
                                break;
                            case 3:
                                builder.AppendLine("열풍 팬 에러", true);
                                break;
                            case 4:
                                builder.AppendLine("열풍 히터 과열", true);
                                break;
                            case 5:
                                builder.AppendLine("열풍 히터 오픈", true);
                                break;
                            case 6:
                                builder.AppendLine("만수, 워터센서 오픈", true);
                                break;
                            case 7:
                                builder.AppendLine("열풍 히터 저온", true);
                                break;
                        }
                    }
                }
            return builder.ToString();
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
    }
}
