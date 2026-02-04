using BliMonitorTest.data;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using BliMonitorTest.util.StoragePathUtil;
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
                    StateBox.cont.Content = "운전중";
                    this.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                    StateBox.cont.Background = new SolidColorBrush(Color.FromRgb(98, 255, 81));
                }
                else
                {
                    StateBox.cont.Content = "정지";
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
            Item1.cont.Content = "";
            Item2.cont.Content = "";
            Item3.cont.Content = "";
            Item4.cont.Content = "";
            Item5.cont.Content = "";
            Item6.cont.Content = "";
            Item11.cont.Content = "";
            Item12.cont.Content = "";
            Item13.cont.Content = "";
            Item14.cont.Content = "";
            Item15.cont.Content = "";
            Item16.cont.Content = "";
            Item17.cont.Content = "";
            Item18.cont.Content = "";
            StateBox.cont.Content = "";
            ModeTimeView.cont.Content = "";
            ModeTimeView.label.Content = "";
            TestTime = TimeSpan.Zero;
        }

        public int Channel
        {
            get { return _Channel; }
            set
            {
                _Channel = value;
                Dispatcher.Invoke(new Action(() => { ChannelView.cont.Content = value; }));
            }
        }

        private void initFile()
        {
            air_sum = 0;
            off_sum = 0;
            number = 0;

            streamWriter.WriteLine("날짜,모드,남은 시간,히터 온도,히터 오프타임,배기온도,FAN Speed," +
                "평균히터오프타임,열풍온타임,MOTOR,모터 전류,번호,오프타임합,오프평균,배기 합,배기 평균");
        }

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

                    // 2) SQLite 기록 : Queue 방식으로 비동기 처리
                    MonitoringDbWriteService.Instance.Start(StoragePathUtil.GetDbPath());
                    MonitoringDbWriteService.Instance.Enqueue(data, number, off_sum, air_sum, channelNo: 1, sourceType: MonitoringDb.SOURCE_SINGLE);
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
                    //MessageBox.Show("연결 되지 않았습니다.");
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
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.Connected)
            {
                //MessageBox.Show("연결 되지 않았습니다.");
                ToastMessage.ToastService.AppToast.Show("연결 되지 않았습니다.");
                return;
            }
            run = false;

            byte[] command = null;

            command = Protocol.GetNewCommand(3);

            try
            {
                client.GetStream().Write(command, 0, command.Length);
            }
            catch (Exception ex)
            {
                client.Close();
                Console.WriteLine(ex.ToString());
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.Connected)
            {
                //MessageBox.Show("연결 되지 않았습니다.");
                ToastMessage.ToastService.AppToast.Show("연결 되지 않았습니다.");
                return;
            }
            byte[] command = null;
            if (run)
            {
                //MessageBox.Show("이미 시작했습니다.");
                ToastMessage.ToastService.AppToast.Show("이미 시작했습니다.");
                return;
            }

            command = Protocol.GetNewCommand(2);

            try
            {
                client.GetStream().Write(command, 0, command.Length);
            }
            catch (Exception ex)
            {
                client.Close();
                Console.WriteLine(ex.ToString());
            }
            //ClearPoints();
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

            int motorRun = data[5];
            int heateroff = data[7];        //히터 오프타임
            int heatertemp = data[6];       //히터 온도
            int airtemp = data[8];          //배기 온도
            int airaverage = data[9];
            int airheatertemp = data[10];   //열풍 온도
            Console.WriteLine("10: {0}", airheatertemp);
            int heaterduty = data[11];
            int mode = data[15];
            int minute = data[16];
            int second = data[17];
            if (mode == 7)
            {
                if (run)
                {
                    run = false;
                }
            }
            else
            {
                if (!run)
                {
                    run = true;
                }
            }
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

            //total_minute += remain_second;
            int t_hour = data[18];
            int t_min = data[19];
            int t_sec = data[20];
            int runTime = data[21];
            int fan_duty = data[29];
            string time = string.Format("{0:D2}:{1:D2}", minute, second);
            string t_time = string.Format("{0:D2}:{1:D2}:{2:D2}", t_hour, t_min, t_sec);
            byte[] current = { 0, 0, data[13], data[14] };
            Array.Reverse(current);
            int currentInt = BitConverter.ToInt16(current, 0) * 10;
            float currnetDouble = (float)currentInt / 1000.0f;
            float currentfloat = (data[13] & 0xFF) << 8;
            currentfloat += (data[14] & 0xFF);
            currentfloat = currentfloat / 100.0f;
            if (currentfloat > 1)
                currentfloat = -1;
            int year = data[51];
            int month = data[52];
            int day = data[53];
            int version = data[54];
            int model = data[48];
            string micom = $"{year}.{month}.{day} ver {version}";
            byte error0 = data[49];
            byte error1 = data[50];
            int hot_air_fan_duty = data[30];
            int[] binary0 = Enumerable.Range(1, 8).Select(i => error0 / (1 << (8 - i)) % 2).ToArray();
            int[] binary1 = Enumerable.Range(1, 8).Select(i => error1 / (1 << (8 - i)) % 2).ToArray();
            float heateroffTime = (float)(heateroff / 10.0f);
            float averOffTime = (float)(airheatertemp / 10.0f);
            bool[] errors0 = new bool[8];
            bool[] errors1 = new bool[8];
            int motorRunTime = data[12];

            for (int i = 0; i < 8; i++)
            {
                if (binary0[i] == 0)
                {
                    errors0[i] = true;
                }
                else
                {
                    errors0[i] = false;
                }
                if (binary1[i] == 0)
                {
                    errors1[i] = true;
                }
                else
                {
                    errors1[i] = false;
                }
            }
            if (errors1[6])
            {
                Item16.cont.Content = "정상";
            }
            else
            {
                Item16.cont.Content = "감지";
            }

            ReadData read = new ReadData()
            {
                date = DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss"),
                mode = mode + 1,
                remain_time = time,
                heater_temp = heatertemp,
                heater_off_time = heateroff,
                air_temp = airtemp,
                fan_speed = fan_duty,
                hot_air_temp = airheatertemp,
                hot_air_ontime = heaterduty,
                motor = getMotorState(motorRun),
                motor_current = currentfloat / 2.0
            };

            // 엑셀파일 생성
            WriteFile(read);

            string errorStr = GetErrorName(binary0, binary1);
            StateContent.Content = errorStr;

            Item1.cont.Content = heatertemp + "ºC";
            Item2.cont.Content = airtemp + "ºC";
            Item3.cont.Content = averOffTime + "ms";
            Item4.Title = getMotorState(motorRun);
            Item4.cont.Content = motorRunTime.ToString() + "s";
            Item5.cont.Content = fan_duty + "%";
            Item6.cont.Content = t_time;
            Item11.cont.Content = heateroffTime + "ms";
            Item12.cont.Content = airaverage + "ºC";
            Item13.cont.Content = heaterduty;
            Item14.cont.Content = string.Format("{0:0.00A}", currentfloat / 2.0);
            Item15.cont.Content = hot_air_fan_duty + "%";
            Item17.cont.Content = micom;
            Item18.cont.Content = getModelName(model);

            ModeTimeView.cont.Content = time;
            ModeTimeView.label.Content = string.Format("모드{0}", mode + 1);
            DateTime now = DateTime.Now;

            if (Item1Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10], new DataPoint(total_minute, heatertemp));
            }
            if (Item2Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 1], new DataPoint(total_minute, airtemp));
            }
            if (Item3Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 2], new DataPoint(total_minute, airheatertemp));
            }
            if (Item4Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 3], new DataPoint(total_minute, getMotorValue(motorRun)));
            }
            if (Item5Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 4], new DataPoint(total_minute, heateroff));
            }
            if (Item6Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 5], new DataPoint(total_minute, airaverage));
            }
            if (Item7Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 6], new DataPoint(total_minute, heaterduty));
            }
            if (Item8Check.IsChecked.Value)
            {
                chartView.ViewModel.AddData(seriesList[ItemIndex * 10 + 7], new DataPoint(total_minute, currnetDouble / 2.0));
            }

            lock (chartView.ViewModel)
            {
                //log.Debug($"isDummy={IsDummyEnabled} total_second={total_second} total_minute={total_minute:F3} start={_dummyStartTime:HH:mm:ss.fff}");
                chartView.ViewModel.panXAxis(total_minute);
            }
        }

        private string getModelName(int model)
        {
            switch (model)
            {
                case 0:
                    return "PCS400";
                case 1:
                    return "PCS500";
                case 2:
                    return "PCS350";
                case 3:
                    return "PCS400_T";
                case 4:
                    return "PCS400P";
                case 5:
                    return "PCS400P_T";
                case 6:
                    return "PCS500N";
                case 7:
                    return "PCS400A_T_E";
                case 8:
                    return "PCS400P_T_E";
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
