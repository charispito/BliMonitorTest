using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.setting;
using BliMonitorTest.ToastMessage;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using log4net;

namespace BliMonitorTest
{
    /// <summary>
    /// MultiParameterWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MultiParameterWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MultiParameterWindow));

        private bool Errorset = false;
        private ChannelItem channelItem = null;
        private bool RightSet = false;
        private List<string> files;
        private List<SettingData> mode1 = new List<SettingData>();
        private List<SettingData> mode2 = new List<SettingData>();
        private List<SettingData> mode3 = new List<SettingData>();
        private List<SettingData> mode4 = new List<SettingData>();
        private List<SettingData> mode5 = new List<SettingData>();
        private List<SettingData> motor = new List<SettingData>();

        private RangeEnabledObservableCollection<SettingData> mode11 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> mode12 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> mode13 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> mode14 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> mode15 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> motor1 = new RangeEnabledObservableCollection<SettingData>();

        private List<SettingData> fan = new List<SettingData>();
        private List<string> errorFiles;
        private RangeEnabledObservableCollection<SettingData> fan1 = new RangeEnabledObservableCollection<SettingData>();
        private List<byte> receivedData = new List<byte>();
        private ConfigFileManagement management;
        private RangeEnabledObservableCollection<SettingData> heater1 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater2 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater11 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater12 = new RangeEnabledObservableCollection<SettingData>();

        // 진행 상태 및 저장 간격 제어
        private volatile bool _isReadingError = false;

        private int retentionDays = 90;     // 에러데이터 보관 기간(일)
        private int maxFiles = 10000;       // 에러데이터 최대 파일 수

        // 자동 저장 디바운스
        private System.Threading.CancellationTokenSource _autoSaveCts;
        private TimeSpan _autoSaveDebounce = TimeSpan.FromMilliseconds(400);

        private ICollectionView _fileView;
        private ICollectionView _errorView;

        // 휴지통 실패 시 하드 삭제 폴백 여부(필요하면 true로 켬)
        private bool _allowHardDeleteFallback = false;

        // 에러 데이터 DB저장 관련 상수
        private int _channelNoForDb = 1;

        public MultiParameterWindow()
        {
            InitializeComponent();
        }

        public MultiParameterWindow(ChannelItem channelItem, int channelNoForDb)
        {
            InitializeComponent();
            management = new ConfigFileManagement();
            this.channelItem = channelItem;
            this._channelNoForDb = channelNoForDb;
            Initialize();
            SetList();
            SetErrorList();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += MultiParameterWindow_Loaded;
        }

        private void MultiParameterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InvalidateVisual();
        }

        private void SetList()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParameterSetting");

            try
            {
                if (!Directory.Exists(path))
                {
                    files = new List<string>();
                    files.Clear();
                    FileList.ItemsSource = files;
                    FileList.Items.Refresh();
                    return;
                }

                var ordered = Directory.EnumerateFiles(path, "*.config")
                                       .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                                       .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                       .ToList();

                files = new List<string>();
                files.Clear();
                files.AddRange(ordered);

                FileList.ItemsSource = files;
                FileList.Items.Refresh();
            }
            catch (Exception ex)
            {
                log.Warn("SetList 처리 중 문제", ex);
                try
                {
                    var info = new DirectoryInfo(path);
                    files = new List<string>();
                    files.Clear();
                    foreach (var fi in info.GetFiles("*.config"))
                        files.Add(System.IO.Path.GetFileNameWithoutExtension(fi.Name));
                    FileList.ItemsSource = files;
                    FileList.Items.Refresh();
                }
                catch
                {
                    files = new List<string>();
                    files.Clear();
                    FileList.ItemsSource = files;
                    FileList.Items.Refresh();
                }
            }

            InitCollectionViews();
            ApplyFileFilter((FileSearchBox != null) ? FileSearchBox.Text : null);
        }

        private void SetErrorList()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");

            try
            {
                if (!Directory.Exists(path))
                {
                    errorFiles = new List<string>();
                    errorFiles.Clear();
                    ErrorFileList.ItemsSource = errorFiles;
                    ErrorFileList.Items.Refresh();
                    return;
                }

                var ordered = Directory.EnumerateFiles(path, "*.config")
                                       .Select(p => new FileInfo(p))
                                       .OrderByDescending(fi => fi.CreationTimeUtc)
                                       .Select(fi => System.IO.Path.GetFileNameWithoutExtension(fi.Name))
                                       .ToList();

                errorFiles = new List<string>();
                errorFiles.Clear();
                errorFiles.AddRange(ordered);

                ErrorFileList.ItemsSource = errorFiles;
                ErrorFileList.Items.Refresh();
            }
            catch (Exception ex)
            {
                log.Warn("SetErrorList 정렬 처리 중 문제 발생", ex);
                try
                {
                    var info = new DirectoryInfo(path);
                    errorFiles = new List<string>();
                    errorFiles.Clear();
                    foreach (FileInfo file in info.GetFiles("*.config"))
                        errorFiles.Add(System.IO.Path.GetFileNameWithoutExtension(file.Name));
                    ErrorFileList.ItemsSource = errorFiles;
                    ErrorFileList.Items.Refresh();
                }
                catch
                {
                    errorFiles = new List<string>();
                    errorFiles.Clear();
                    ErrorFileList.ItemsSource = errorFiles;
                    ErrorFileList.Items.Refresh();
                }
            }

            InitCollectionViews();
            ApplyErrorFilter((ErrorSearchBox != null) ? ErrorSearchBox.Text : null);
        }


        private void ListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            FileName.Text = item.Content.ToString();
            SettingItem setting = management.ReadFromFile(item.Content.ToString());
            mode11.Clear();
            mode12.Clear();
            mode13.Clear();
            mode14.Clear();
            mode15.Clear();
            motor1.Clear();
            fan1.Clear();
            heater11.Clear();
            heater12.Clear();
            RightSet = true;
            foreach (SectionItem section in setting.mode1)
            {
                mode11.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.mode2)
            {
                mode12.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.mode3)
            {
                mode13.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.mode4)
            {
                mode14.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.mode5)
            {
                mode15.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.motor)
            {
                motor1.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.fan)
            {
                fan1.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.heater1)
            {
                heater11.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.heater2)
            {
                heater12.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
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
                                return "모터 과부하";
                            //builder.AppendLine("모터 과부하", true);
                            //break;
                            case 1:
                                return "모터 단선";
                            //builder.AppendLine("모터 단선", true);
                            //break;
                            case 2:
                                return "히터 동작 이상";
                            //builder.AppendLine("히터 동작 이상", true);
                            //break;
                            case 3:
                                return "히터 동작 이상";
                            //if (errors0[2] != 1)
                            //    builder.AppendLine("히터 동작 이상", true);
                            //else
                            //    cnt--;
                            //break;
                            case 4:
                                return "히터 센서 이상";
                            //builder.AppendLine("히터 센서 이상", true);
                            //break;
                            case 5:
                                return "배기 온도 이상";
                            //builder.AppendLine("배기 온도 이상", true);
                            //break;
                            case 6:
                                return "배기 센서 이상";
                            //builder.AppendLine("배기 센서 이상", true);
                            //break;
                            case 7:
                                return "배기 팬 이상";
                                //builder.AppendLine("배기 팬 이상", true);
                                //break;
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
                                return "이물질감지";
                            //builder.AppendLine("이물질감지", true);
                            //break;
                            case 1:
                                return "도어 열림";
                            //builder.AppendLine("도어 열림", true);
                            //break;
                            case 2:
                                return "도어 열림";
                            //if (errors1[1] != 1)
                            //    builder.AppendLine("도어 열림", true);
                            //else
                            //    cnt--;
                            //break;
                            case 3:
                                return "열풍 팬 에러";
                            //builder.AppendLine("열풍 팬 에러", true);
                            //break;
                            case 4:
                                return "열풍 히터 과열";
                            //builder.AppendLine("열풍 히터 과열", true);
                            //break;
                            case 5:
                                return "열풍 히터 오픈";
                            //builder.AppendLine("열풍 히터 오픈", true);
                            //break;
                            case 6:
                                return "만수, 워터센서 오픈";
                            //builder.AppendLine("만수, 워터센서 오픈", true);
                            //break;
                            case 7:
                                return "열풍 히터 저온";
                                //builder.AppendLine("열풍 히터 저온", true);
                                //break;
                        }
                    }
                }
            return builder.ToString();
        }

        private void WriteParamButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RightSet)
            {
                //MessageBox.Show("값이 설정 되지 않았습니다.");
                ToastMessage.ToastService.AppToast.Show("값이 설정 되지 않았습니다.");
                return;
            }
            byte[] command = GetParameterSettingData();
            channelItem.client.GetStream().Write(command, 0, command.Length);
            GetParameterSettingData().PrintHex(1);
        }
        public void setParameter(byte[] data)
        {
            if (data.Length != 70)
            {
                return;
            }
            int onTimeCW1 = data[4];
            int offTimeCW1 = data[5];
            int onTimeCCW1 = data[6];
            int offTimeCCW1 = data[7];
            int HeaterTemp1 = data[8];
            int HeaterOffTime1 = data[9];
            int VentileTemp1 = data[10];
            int OperateTime1 = data[11];
            int ExhaustFanWaitMode = data[12];
            if(mode1 == null)
            {
                mode1 = new List<SettingData>();
            }
            mode1.Clear();

            mode1.Add(new SettingData() { Name = "ON TIME CW", Value = onTimeCW1 });
            mode1.Add(new SettingData() { Name = "OFF TIME CW", Value = offTimeCW1 });
            mode1.Add(new SettingData() { Name = "ON TIME CCW", Value = onTimeCCW1 });
            mode1.Add(new SettingData() { Name = "OFF TIME CCW", Value = offTimeCCW1 });
            mode1.Add(new SettingData() { Name = "HEATER TEMP", Value = HeaterTemp1 });
            mode1.Add(new SettingData() { Name = "HEATER OFF TIME", Value = HeaterOffTime1 });
            mode1.Add(new SettingData() { Name = "VENTILE TEMP", Value = VentileTemp1 });
            mode1.Add(new SettingData() { Name = "OPERATE TIME", Value = OperateTime1 });

            int onTimeCW2 = data[14];
            int offTimeCW2 = data[15];
            int onTimeCCW2 = data[16];
            int offTimeCCW2 = data[17];
            int HeaterTemp2 = data[18];
            int HeaterOffTime2 = data[19];
            int VentileTemp2 = data[20];
            int OperateTime2 = data[21];
            int ExhaustFanOperateMode = data[22];

            if (fan == null)
            {
                fan = new List<SettingData>();
            }
            fan.Clear();

            fan.Add(new SettingData() { Name = "배기 FAN 대기 모드", Value = ExhaustFanWaitMode });
            fan.Add(new SettingData() { Name = "배기 FAN 운전 모드", Value = ExhaustFanOperateMode });

            if (mode2 == null)
            {
                mode2 = new List<SettingData>();
            }
            mode2.Clear();

            mode2.Add(new SettingData() { Name = "ON TIME CW", Value = onTimeCW2 });
            mode2.Add(new SettingData() { Name = "OFF TIME CW", Value = offTimeCW2 });
            mode2.Add(new SettingData() { Name = "ON TIME CCW", Value = onTimeCCW2 });
            mode2.Add(new SettingData() { Name = "OFF TIME CCW", Value = offTimeCCW2 });
            mode2.Add(new SettingData() { Name = "HEATER TEMP", Value = HeaterTemp2 });
            mode2.Add(new SettingData() { Name = "HEATER OFF TIME", Value = HeaterOffTime2 });
            mode2.Add(new SettingData() { Name = "VENTILE TEMP", Value = VentileTemp2 });
            mode2.Add(new SettingData() { Name = "OPERATE TIME", Value = OperateTime2 });

            int onTimeCW3 = data[24];
            int offTimeCW3 = data[25];
            int onTimeCCW3 = data[26];
            int offTimeCCW3 = data[27];
            int HeaterTemp3 = data[28];
            int HeaterOffTime3 = data[29];
            int VentileTemp3 = data[30];
            int OperateTime3 = data[31];

            if (mode3 == null)
            {
                mode3 = new List<SettingData>();
            }
            mode3.Clear();

            mode3.Add(new SettingData() { Name = "ON TIME CW", Value = onTimeCW3 });
            mode3.Add(new SettingData() { Name = "OFF TIME CW", Value = offTimeCW3 });
            mode3.Add(new SettingData() { Name = "ON TIME CCW", Value = onTimeCCW3 });
            mode3.Add(new SettingData() { Name = "OFF TIME CCW", Value = offTimeCCW3 });
            mode3.Add(new SettingData() { Name = "HEATER TEMP", Value = HeaterTemp3 });
            mode3.Add(new SettingData() { Name = "HEATER OFF TIME", Value = HeaterOffTime3 });
            mode3.Add(new SettingData() { Name = "VENTILE TEMP", Value = VentileTemp3 });
            mode3.Add(new SettingData() { Name = "OPERATE TIME", Value = OperateTime3 });

            int onTimeCW4 = data[34];
            int offTimeCW4 = data[35];
            int onTimeCCW4 = data[36];
            int offTimeCCW4 = data[37];
            int HeaterTemp4 = data[38];
            int HeaterOffTime4 = data[39];
            int VentileTemp4 = data[40];
            int OperateTime4 = data[41];

            if (mode4 == null)
            {
                mode4 = new List<SettingData>();
            }
            mode4.Clear();

            mode4.Add(new SettingData() { Name = "ON TIME CW", Value = onTimeCW4 });
            mode4.Add(new SettingData() { Name = "OFF TIME CW", Value = offTimeCW4 });
            mode4.Add(new SettingData() { Name = "ON TIME CCW", Value = onTimeCCW4 });
            mode4.Add(new SettingData() { Name = "OFF TIME CCW", Value = offTimeCCW4 });
            mode4.Add(new SettingData() { Name = "HEATER TEMP", Value = HeaterTemp4 });
            mode4.Add(new SettingData() { Name = "HEATER OFF TIME", Value = HeaterOffTime4 });
            mode4.Add(new SettingData() { Name = "VENTILE TEMP", Value = VentileTemp4 });
            mode4.Add(new SettingData() { Name = "OPERATE TIME", Value = OperateTime4 });

            int onTimeCW5 = data[44];
            int offTimeCW5 = data[45];
            int onTimeCCW5 = data[46];
            int offTimeCCW5 = data[47];
            int HeaterTemp5 = data[48];
            int HeaterOffTime5 = data[49];
            int VentileTemp5 = data[50];
            int OperateTime5 = data[51];

            if (mode5 == null)
            {
                mode5 = new List<SettingData>();
            }
            mode5.Clear();

            mode5.Add(new SettingData() { Name = "ON TIME CW", Value = onTimeCW5 });
            mode5.Add(new SettingData() { Name = "OFF TIME CW", Value = offTimeCW5 });
            mode5.Add(new SettingData() { Name = "ON TIME CCW", Value = onTimeCCW5 });
            mode5.Add(new SettingData() { Name = "OFF TIME CCW", Value = offTimeCCW5 });
            mode5.Add(new SettingData() { Name = "HEATER TEMP", Value = HeaterTemp5 });
            mode5.Add(new SettingData() { Name = "HEATER OFF TIME", Value = HeaterOffTime5 });
            mode5.Add(new SettingData() { Name = "VENTILE TEMP", Value = VentileTemp5 });
            mode5.Add(new SettingData() { Name = "OPERATE TIME", Value = OperateTime5 });

            int motor1 = data[54];
            int motor2 = data[55];
            int motor3 = data[56];
            int motor4 = data[57];
            int motor5 = data[58];

            if (motor == null)
            {
                motor = new List<SettingData>();
            }
            motor.Clear();

            motor.Add(new SettingData() { Name = "이물질 감지 시간", Value = motor1 });
            motor.Add(new SettingData() { Name = "이물질 감지 전류", Value = motor2 });
            motor.Add(new SettingData() { Name = "이물질 감지 횟수", Value = motor3 });
            motor.Add(new SettingData() { Name = "과부하 감지 전류", Value = motor4 });
            motor.Add(new SettingData() { Name = "과부하 감지 횟수", Value = motor5 });
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MicomGrid.ItemsSource = null;
                MicomGrid2.ItemsSource = null;
                MicomGrid3.ItemsSource = null;
                MicomGrid4.ItemsSource = null;
                MicomGrid5.ItemsSource = null;
                MotorGrid.ItemsSource = null;
                FanGrid.ItemsSource = null;


                MicomGrid.ItemsSource = mode1;
                MicomGrid2.ItemsSource = mode2;
                MicomGrid3.ItemsSource = mode3;
                MicomGrid4.ItemsSource = mode4;
                MicomGrid5.ItemsSource = mode5;
                MotorGrid.ItemsSource = motor;
                FanGrid.ItemsSource = fan;
            }));
            
        }

        private void Initialize()
        {
            ObservableCollection<SettingData> list = new ObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "에러 내용" });
            list.Add(new SettingData() { Name = "운전 모드" });
            list.Add(new SettingData() { Name = "히터 온도" });
            list.Add(new SettingData() { Name = "히터 오프 타임" });
            list.Add(new SettingData() { Name = "배기 온도" });
            list.Add(new SettingData() { Name = "열풍 온도" });
            list.Add(new SettingData() { Name = "열풍 On Time" });
            list.Add(new SettingData() { Name = "운전 횟수" });
            ErrorGrid.ItemsSource = list;
            MicomGrid.ItemsSource = getModeData();
            MicomGrid2.ItemsSource = getModeData();
            MicomGrid3.ItemsSource = getModeData();
            MicomGrid4.ItemsSource = getModeData();
            MicomGrid5.ItemsSource = getModeData();
            MotorGrid.ItemsSource = getMotorData();
            mode11 = getModeData();
            mode12 = getModeData();
            mode13 = getModeData();
            mode14 = getModeData();
            mode15 = getModeData();
            SetGrid1.ItemsSource = mode11;
            SetGrid2.ItemsSource = mode12;
            SetGrid3.ItemsSource = mode13;
            SetGrid4.ItemsSource = mode14;
            SetGrid5.ItemsSource = mode15;
            motor1 = getMotorData();
            fan1 = getFanData();
            heater11 = getHeaterData();
            heater12 = getHeaterData2();
            SetGrid6.ItemsSource = motor1;
            HeaterGridSetting.ItemsSource = heater11;
            FanGrid2.ItemsSource = fan1;
            HeaterGridSetting2.ItemsSource = heater12;
            HeaterGrid.ItemsSource = getHeaterData();
            FanGrid.ItemsSource = getFanData();
            HeaterGrid2.ItemsSource = getHeaterData2();
            ComplieGrid.ItemsSource = getCompileData(2022, 1, 1, 0);
            Loaded += ParameterWindow_Loaded;
            Closed += ParameterWindow_Closed;
            RightButton.Click += RightButton_Click;
            ReadParamButton.Click += ReadParamButton_Click;
            SaveButton.Click += SaveButton_Click;
            DeleteButton.Click += DeleteButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            ErrorSaveButton.Click += ErrorSaveButton_Click;
            ErrorDeleteButton.Click += ErrorDeleteButton_Click;
            ErrorRefreshButton.Click += ErrorRefreshButton_Click;
            //OpenErrorDataQueryButton.Click += OpenErrorDataQueryButton_Click;
            
            // 검색 필터 이벤트 연결 (XAML의 x:Name 동일 가정)
            FileSearchBox.TextChanged += FileSearchBox_TextChanged;
            ErrorSearchBox.TextChanged += ErrorSearchBox_TextChanged;

            ReadErrorButton.Click += (s, e) =>
            {
                if (_isReadingError)
                {
                    log.Info("ReadErrorButton: 이전 요청 처리 중. 클릭 무시");
                    return;
                }

                try
                {
                    byte[] command = Protocol.GetError();
                    command.PrintHex(1);
                    channelItem.client.GetStream().Write(command, 0, command.Length);
                    Errorset = true;
                    channelItem.setParameter(1);
                } catch (Exception ex) {
                    log.Error("ReadErrorButton 전송 실패", ex);
                    _isReadingError = false;
                    ReadErrorButton.IsEnabled = true;

                    //MessageBox.Show("에러 요청 전송 중 문제가 발생했습니다.");
                    ToastMessage.ToastService.AppToast.Show("에러 요청 전송 중 문제가 발생했습니다.");
                } finally {
                    // 향후 패킷수신시 자동 저장 기능 구현 예정
                    AutoSaveErrorDataToFileDebounced();

                    // 요청 상태 해제
                    _isReadingError = false;
                    ReadErrorButton.IsEnabled = true;
                }
            };

            WriteParamButton.Click += WriteParamButton_Click;
            ResetErrorButton.Click += ResetErrorButton_Click;

            InitCollectionViews();
            this.KeyDown += Window_KeyDown;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (FileList.IsKeyboardFocusWithin)
                {
                    SelectAllInListBox(FileList);
                    e.Handled = true;
                }
                else if (ErrorFileList.IsKeyboardFocusWithin)
                {
                    SelectAllInListBox(ErrorFileList);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Delete)
            {
                bool hard = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                if (FileList.IsKeyboardFocusWithin && FileList.SelectedItems.Count > 0)
                {
                    DeleteSelectedCore(FileList.SelectedItems, hardDelete: hard, kind: "Param");
                    e.Handled = true;
                }
                else if (ErrorFileList.IsKeyboardFocusWithin && ErrorFileList.SelectedItems.Count > 0)
                {
                    DeleteSelectedCore(ErrorFileList.SelectedItems, hardDelete: hard, kind: "Error");
                    e.Handled = true;
                }
            }
        }

        private void SelectAllInListBox(ListBox lb)
        {
            if (lb.SelectionMode != SelectionMode.Extended) return;
            lb.SelectedItems.Clear();
            foreach (var item in lb.Items)
                lb.SelectedItems.Add(item);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetList();
            SetErrorList();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedCore(FileList.SelectedItems, hardDelete: false, kind: "Param");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileName.Text.Length == 0)
            {
                //MessageBox.Show("파일명을 입력 하세요");
                ToastMessage.ToastService.AppToast.Show("파일명을 입력 하세요.");
                return;
            }
            else
            {
                SettingItem item = GetSettingSectionData();
                management.CreateConfig(FileName.Text.ToString(), item);
                SetList();
                SetErrorList();
                //MessageBox.Show("저장 되었습니다.");
                ToastMessage.ToastService.AppToast.Show("저장 되었습니다.");
                
            }
        }

        private void ErrorListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListBoxItem;
            if (item == null) return;

            var content = item.Content;
            if (content == null) return;
            string name = content.ToString();
            if (name.Length == 0) return;

            ErrorFileName.Text = name;

            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
            string full = System.IO.Path.Combine(path, name + ".config");
            if (!File.Exists(full)) return;

            var list = new ObservableCollection<ErrorData>();

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(full);
                var root = doc.Root;
                if (root == null)
                {
                    //MessageBox.Show("잘못된 파일 형식입니다.");
                    ToastMessage.ToastService.AppToast.Show("잘못된 파일 형식입니다.");
                    return;
                }

                var section = root.Element("Error");
                if (section == null)
                {
                    //MessageBox.Show("Error 섹션을 찾을 수 없습니다.");
                    ToastMessage.ToastService.AppToast.Show("Error 섹션을 찾을 수 없습니다.");
                    return;
                }

                foreach (var add in section.Elements("add"))
                {
                    string n = "";
                    string val1 = "";
                    string val2 = "";
                    string val3 = "";
                    string val4 = "";
                    string val5 = "";

                    var attrN = add.Attribute("Name");
                    if (attrN != null) n = attrN.Value;

                    var attr1 = add.Attribute("Value1");
                    if (attr1 != null) val1 = NormalizeZero(attr1.Value);

                    var attr2 = add.Attribute("Value2");
                    if (attr2 != null) val2 = NormalizeZero(attr2.Value);

                    var attr3 = add.Attribute("Value3");
                    if (attr3 != null) val3 = NormalizeZero(attr3.Value);

                    var attr4 = add.Attribute("Value4");
                    if (attr4 != null) val4 = NormalizeZero(attr4.Value);

                    var attr5 = add.Attribute("Value5");
                    if (attr5 != null) val5 = NormalizeZero(attr5.Value);

                    list.Add(new ErrorData
                    {
                        Name = n,
                        Value = val1,
                        Value2 = val2,
                        Value3 = val3,
                        Value4 = val4,
                        Value5 = val5
                    });
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    ErrorGrid.ItemsSource = list;
                }));
            }
            catch (Exception ex)
            {
                log.Error("Error 파일 로드 실패", ex);
                //MessageBox.Show("에러 파일을 읽는 중 문제가 발생했습니다.");
                ToastMessage.ToastService.AppToast.Show("에러 파일을 읽는 중 문제가 발생했습니다.");
            }
        }

        // 문자열이 "0" 또는 공백+0 형태면 "0"으로, 숫자 0도 "0"으로 표시.
        // 숫자가 아니거나 빈 문자열은 원문 유지.
        // 문자열이 "0" 또는 공백+0 형태면 "0"으로, 숫자 0도 "0"으로 표시.
        // 숫자가 아니거나 빈 문자열은 원문 유지.
        private static string NormalizeZero(string s)
        {
            if (s == null) return "";
            string t = s.Trim();

            // 빈 문자열은 그대로 빈 문자열로 둔다 (사용자가 비워둔 칸 구분용)
            if (t.Length == 0) return "";

            int num;
            // 숫자로 파싱 가능하면 0일 때 "0"
            if (int.TryParse(t, out num))
            {
                if (num == 0) return "0";
                return t; // 0이 아니면 원문 유지 (예: "5")
            }

            // 숫자가 아니면 원문 유지 (에러 텍스트 등)
            return t;
        }

        private void ErrorSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ErrorFileName.Text == null || ErrorFileName.Text.Trim().Length == 0)
            {
                //MessageBox.Show("에러 파일명을 입력하세요.");
                ToastMessage.ToastService.AppToast.Show("에러 파일명을 입력하세요.");
                return;
            }

            var src = ErrorGrid.ItemsSource as System.Collections.IEnumerable;
            if (src == null)
            {
                //MessageBox.Show("저장할 에러 데이터가 없습니다.");
                ToastMessage.ToastService.AppToast.Show("저장할 에러 데이터가 없습니다.");
                return;
            }

            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string full = System.IO.Path.Combine(dir, ErrorFileName.Text + ".config");

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<configuration>");
            sb.AppendLine("  <configSections>");
            sb.AppendLine("    <section name=\"Error\" type=\"BliMonitorTest.setting.SettingSection, BliMonitorTest, Version=1.0.0.9, Culture=neutral, PublicKeyToken=null\" />");
            sb.AppendLine("  </configSections>");
            sb.AppendLine("  <appSettings>");
            sb.AppendLine("    <clear />");
            sb.AppendLine("  </appSettings>");
            sb.AppendLine("  <Error>");

            int index = 1;
            foreach (var row in src)
            {
                string name = "";
                string v1 = "";
                string v2 = "";
                string v3 = "";
                string v4 = "";
                string v5 = "";

                // ErrorData인 경우
                var ed = row as ErrorData;
                if (ed != null)
                {
                    name = ed.Name ?? "";
                    v1 = ed.Value ?? "";
                    v2 = ed.Value2 ?? "";
                    v3 = ed.Value3 ?? "";
                    v4 = ed.Value4 ?? "";
                    v5 = ed.Value5 ?? "";
                }
                else
                {
                    // SettingData인 경우
                    var sd = row as SettingData;
                    if (sd != null)
                    {
                        name = sd.Name ?? "";

                        // Value는 반드시 문자열화
                        v1 = sd.Value.ToString();
                        v2 = sd.Value2.ToString();
                        v3 = sd.Value3.ToString();
                        v4 = sd.Value4.ToString();
                        v5 = sd.Value5.ToString();
                    }
                    else
                    {
                        continue;
                    }
                }

                // 저장용 0 정규화
                v1 = NormalizeZeroForSave(v1);
                v2 = NormalizeZeroForSave(v2);
                v3 = NormalizeZeroForSave(v3);
                v4 = NormalizeZeroForSave(v4);
                v5 = NormalizeZeroForSave(v5);

                // XML 이스케이프 (string만)
                name = System.Security.SecurityElement.Escape(name);
                v1 = System.Security.SecurityElement.Escape(v1);
                v2 = System.Security.SecurityElement.Escape(v2);
                v3 = System.Security.SecurityElement.Escape(v3);
                v4 = System.Security.SecurityElement.Escape(v4);
                v5 = System.Security.SecurityElement.Escape(v5);

                // 단 한 번만 AppendLine
                sb.AppendLine("    <add Name=\"" + name + "\" Value1=\"" + v1 + "\" Value2=\"" + v2 + "\" Value3=\"" + v3 + "\" Value4=\"" + v4 + "\" Value5=\"" + v5 + "\" Index=\"" + index.ToString() + "\" />");
                index++;
            }

            sb.AppendLine("  </Error>");
            sb.AppendLine("</configuration>");

            File.WriteAllText(full, sb.ToString(), Encoding.UTF8);
            SetErrorList();

            // 에러데이터 DB에 적재
            string fileOnly = System.IO.Path.GetFileNameWithoutExtension(full);
            //EnqueueErrorEventsFromGrid(fileNameForSnapshot: fileOnly, sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_MULTI, channelNo: _channelNoForDb);

            ToastMessage.ToastService.AppToast.Show("에러 데이터가 저장되었습니다.");
        }

        // null→"0"; trim 후 빈칸→"0"; 숫자면 0→"0", 그 외는 원문 유지; 숫자 아님은 원문 유지
        private static string NormalizeZeroForSave(string s)
        {
            if (s == null) return "0";
            string t = s.Trim();
            if (t.Length == 0) return "0"; // 빈칸도 0으로 저장

            int num;
            if (int.TryParse(t, out num))
            {
                return (num == 0) ? "0" : t;
            }
            return t;
        }

        private void ErrorRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetErrorList();
        }

        private void ErrorDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedCore(ErrorFileList.SelectedItems, hardDelete: false, kind: "Error");
        }

        private SettingItem GetSettingSectionData()
        {
            SettingItem items = null;
            List<SectionItem> section1 = new List<SectionItem>();
            List<SectionItem> section2 = new List<SectionItem>();
            List<SectionItem> section3 = new List<SectionItem>();
            List<SectionItem> section4 = new List<SectionItem>();
            List<SectionItem> section5 = new List<SectionItem>();
            List<SectionItem> section6 = new List<SectionItem>();
            List<SectionItem> section7 = new List<SectionItem>();
            List<SectionItem> section8 = new List<SectionItem>();
            List<SectionItem> section9 = new List<SectionItem>();
            int index = 0;
            foreach (SettingData setting in mode11)
            {
                section1.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in mode12)
            {
                section2.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in mode13)
            {
                section3.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in mode14)
            {
                section4.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in mode15)
            {
                section5.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in motor1)
            {
                section6.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in fan1)
            {
                section7.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in heater11)
            {
                section8.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }
            index = 0;
            foreach (SettingData setting in heater12)
            {
                section9.Add(new SectionItem() { Name = setting.Name, Index = index.ToString(), Value1 = setting.Value, Value2 = setting.Value2 });
                index++;
            }

            items = new SettingItem() { mode1 = section1, mode2 = section2, mode3 = section3, mode4 = section4, mode5 = section5, motor = section6, fan = section7, heater1 = section8, heater2 = section9 };
            return items;
        }

        private void checkParamterData(byte[] arr)
        {
            //받은것이 없을때
            if (receivedData.Count == 0)
            {
                if (arr[0] == 0x12)
                {                        
                    if (arr.Length > 4 && arr.Length == arr[3] && arr[arr.Length - 1] == 0x34)
                    {
                        setParameter(arr);
                    }
                    else
                    {
                        receivedData.AddRange(arr);
                        if (channelItem.client.GetStream().DataAvailable)
                        {
                            Console.WriteLine("continue");
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkParamterData(slice);
                        }
                        else
                        {
                            Console.WriteLine("failed");
                            receivedData.Clear();
                            byte[] command = Protocol.GetParameter();
                            channelItem.client.GetStream().Write(command, 0, command.Length);
                            command.PrintHex(1);
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkParamterData(slice);

                        }
                    }
                }
            }
            else
            {
                receivedData.AddRange(arr);
                if (receivedData.Count > 4 && receivedData.Count == receivedData[3])
                {
                    if (receivedData.Last() == 0x34)
                    {
                        setParameter(receivedData.ToArray());
                    }
                    else
                    {

                        if (channelItem.client.GetStream().DataAvailable)
                        {
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkParamterData(slice);
                        }
                    }
                }
                else
                {
                    if (channelItem.client.GetStream().DataAvailable)
                    {
                        byte[] buffer = new byte[70];
                        int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                        byte[] slice = buffer.Slice(byteRead);
                        checkParamterData(slice);
                    }
                }
            }
            
        }

        private void checkErrorData(byte[] arr)
        {
            if (receivedData.Count == 0)//받은것이 없을때
            {
                if (arr[0] == 0x12)//head
                {
                    if (arr.Length > 4 && arr.Length == arr[3] && arr[arr.Length - 1] == 0x34)
                    {
                        setError(arr);
                    }
                    else
                    {
                        receivedData.AddRange(arr);

                        if (channelItem.client.GetStream().DataAvailable)
                        {
                            Console.WriteLine("continue");
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkErrorData(slice);
                        }
                        else
                        {
                            Console.WriteLine("failed");
                            receivedData.Clear();
                            byte[] command = Protocol.GetError();
                            channelItem.client.GetStream().Write(command, 0, command.Length);
                            command.PrintHex(1);
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkErrorData(slice);

                        }
                    }
                }
            }
            else
            {
                receivedData.AddRange(arr);
                if (receivedData.Count > 4 && receivedData.Count == receivedData[3])
                {
                    if (receivedData.Last() == 0x34)
                    {
                        setError(receivedData.ToArray());
                    }
                    else
                    {

                        if (channelItem.client.GetStream().DataAvailable)
                        {
                            byte[] buffer = new byte[70];
                            int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                            byte[] slice = buffer.Slice(byteRead);
                            checkErrorData(slice);
                        }
                    }
                }
                else
                {
                    if (channelItem.client.GetStream().DataAvailable)
                    {
                        byte[] buffer = new byte[70];
                        int byteRead = channelItem.client.GetStream().Read(buffer, 0, buffer.Length);
                        byte[] slice = buffer.Slice(byteRead);
                        checkErrorData(slice);
                    }
                }
            }
        }

        private void ReadParamButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = Protocol.GetParameter();
            command.PrintHex(1);
            channelItem.setParameter(0);
            channelItem.client.GetStream().Write(command, 0, command.Length);
            channelItem.client.GetStream().Flush();
        }

        private byte[] GetParameterSettingData()
        {
            byte[] command = new byte[70];
            command[0] = 0x12;
            command[1] = 0x01;

            command[2] = 0x96;
            command[3] = 0x46;
            command[4] = (byte)mode11[0].Value;
            command[5] = (byte)mode11[1].Value;
            command[6] = (byte)mode11[2].Value;
            command[7] = (byte)mode11[3].Value;
            command[8] = (byte)mode11[4].Value;
            command[9] = (byte)mode11[5].Value;
            command[10] = (byte)mode11[6].Value;
            command[11] = (byte)mode11[7].Value;
            command[12] = (byte)fan1[0].Value;
            command[13] = (byte)heater11[0].Value2;
            command[14] = (byte)mode12[0].Value;
            command[15] = (byte)mode12[1].Value;
            command[16] = (byte)mode12[2].Value;
            command[17] = (byte)mode12[3].Value;
            command[18] = (byte)mode12[4].Value;
            command[19] = (byte)mode12[5].Value;
            command[20] = (byte)mode12[6].Value;
            command[21] = (byte)mode12[7].Value;
            command[22] = (byte)fan1[1].Value;
            command[23] = (byte)heater11[1].Value2;
            command[24] = (byte)mode13[0].Value;
            command[25] = (byte)mode13[1].Value;
            command[26] = (byte)mode13[2].Value;
            command[27] = (byte)mode13[3].Value;
            command[28] = (byte)mode13[4].Value;
            command[29] = (byte)mode13[5].Value;
            command[30] = (byte)mode13[6].Value;
            command[31] = (byte)mode13[7].Value;
            command[32] = 0x00;
            command[33] = (byte)heater11[2].Value2;
            command[34] = (byte)mode14[0].Value;
            command[35] = (byte)mode14[1].Value;
            command[36] = (byte)mode14[2].Value;
            command[37] = (byte)mode14[3].Value;
            command[38] = (byte)mode14[4].Value;
            command[39] = (byte)mode14[5].Value;
            command[40] = (byte)mode14[6].Value;
            command[41] = (byte)mode14[7].Value;
            command[42] = 0x00;
            command[43] = (byte)heater11[3].Value2;
            command[44] = (byte)mode15[0].Value;
            command[45] = (byte)mode15[1].Value;
            command[46] = (byte)mode15[2].Value;
            command[47] = (byte)mode15[3].Value;
            command[48] = (byte)mode15[4].Value;
            command[49] = (byte)mode15[5].Value;
            command[50] = (byte)mode15[6].Value;
            command[51] = (byte)mode15[7].Value;
            command[52] = 0x00;
            command[53] = (byte)heater11[4].Value2;
            command[54] = (byte)motor1[0].Value;
            command[55] = (byte)motor1[1].Value;
            command[56] = (byte)motor1[2].Value;
            command[57] = (byte)motor1[3].Value;
            command[58] = (byte)motor1[4].Value;
            command[59] = (byte)heater12[0].Value;
            command[60] = (byte)heater11[0].Value;
            command[61] = (byte)heater11[1].Value;
            command[62] = (byte)heater11[2].Value;
            command[63] = (byte)heater11[3].Value;
            command[64] = (byte)heater11[4].Value;
            command[65] = (byte)heater12[1].Value;
            command[66] = (byte)heater11[5].Value;
            command[67] = 0x00;
            command[68] = Protocol.GetCheckSum(command, 1, 67);
            //if (oneChannel.IsNewVersion)
            //{
            //    command[68] = (byte)(command[68] ^ 0xFF);
            //}
            command[69] = 0xEF;
            command[69] = 0x34;

            return command;
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            RightSet = true;
            mode11.Clear();
            mode12.Clear();
            mode13.Clear();
            mode14.Clear();
            mode15.Clear();
            motor1.Clear();
            fan1.Clear();
            mode11.AddRange(mode1);
            mode12.AddRange(mode2);
            mode13.AddRange(mode3);
            mode14.AddRange(mode4);
            mode15.AddRange(mode5);
            motor1.AddRange(motor);
            fan1.AddRange(fan);

            SetGrid1.ItemsSource = null;
            SetGrid2.ItemsSource = null;
            SetGrid3.ItemsSource = null;
            SetGrid4.ItemsSource = null;
            SetGrid5.ItemsSource = null;
            SetGrid6.ItemsSource = null;
            FanGrid2.ItemsSource = null;

            SetGrid1.ItemsSource = mode11;
            SetGrid2.ItemsSource = mode12;
            SetGrid3.ItemsSource = mode13;
            SetGrid4.ItemsSource = mode14;
            SetGrid5.ItemsSource = mode15;
            SetGrid6.ItemsSource = motor1;
            FanGrid2.ItemsSource = fan1;
        }

        private void ParameterWindow_Closed(object sender, EventArgs e)
        {
            channelItem.ParameterMode = false;
            channelItem.ParameterCount = 0;
        }

        private void ParameterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            channelItem.ParameterMode = true;
            byte[] command = Protocol.GetParameter();
        }

        private void ResetErrorButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = null;
            command = Protocol.GetErrorReset();

            command.PrintHex(1);

            channelItem.client.GetStream().Write(command, 0, command.Length);
            channelItem.client.GetStream().Flush();
        }



        public void setError(byte[] data)
        {
            Duo8ErrorResponse resp = Duo8PacketParser.ParseErrorResponse(data);
            if (resp == null)
                return;

            string GetValid(int idx) => resp.Records.Count > idx ? (resp.Records[idx].IsValid ? "Y" : "N") : "";
            string GetSeq(int idx) => resp.Records.Count > idx ? resp.Records[idx].Sequence.ToString() : "";
            string GetErrorCode(int idx) => resp.Records.Count > idx ? $"0x{resp.Records[idx].ErrorCode:X2}" : "";
            string GetErrorName(int idx) => resp.Records.Count > idx ? Duo8ValueText.GetErrorText(resp.Records[idx].ErrorCode) : "";
            string GetHotTemp(int idx) => resp.Records.Count > idx ? resp.Records[idx].HotTempRaw.ToString() : "";
            string GetColdTemp(int idx) => resp.Records.Count > idx ? resp.Records[idx].ColdTempRaw.ToString() : "";
            string GetAdcHot(int idx) => resp.Records.Count > idx ? resp.Records[idx].AdcHotRaw.ToString() : "";
            string GetAdcCold(int idx) => resp.Records.Count > idx ? resp.Records[idx].AdcColdRaw.ToString() : "";
            string GetWaterInitDone(int idx) => resp.Records.Count > idx ? Duo8ValueText.ToYesNo(resp.Records[idx].WaterInitDone) : "";
            string GetStatusARaw(int idx) => resp.Records.Count > idx ? $"0x{resp.Records[idx].StatusA:X2}" : "";
            string GetStatusBRaw(int idx) => resp.Records.Count > idx ? $"0x{resp.Records[idx].StatusB:X2}" : "";
            string GetBufferLow(int idx) => resp.Records.Count > idx ? Duo8ValueText.ToYesNo(resp.Records[idx].BufferLow) : "";

            string GetStatusABit(int idx, byte mask)
                => resp.Records.Count > idx ? Duo8ValueText.GetBitState((resp.Records[idx].StatusA & mask) != 0) : "";

            string GetStatusBBit(int idx, byte mask)
                => resp.Records.Count > idx ? Duo8ValueText.GetBitState((resp.Records[idx].StatusB & mask) != 0) : "";

            ObservableCollection<ErrorData> list = new ObservableCollection<ErrorData>();

            list.Add(new ErrorData()
            {
                Name = "유효",
                Value = GetValid(0),
                Value2 = GetValid(1),
                Value3 = GetValid(2),
                Value4 = GetValid(3),
                Value5 = GetValid(4),
                Value6 = GetValid(5),
                Value7 = GetValid(6),
                Value8 = GetValid(7)
            });

            list.Add(new ErrorData()
            {
                Name = "SEQ",
                Value = GetSeq(0),
                Value2 = GetSeq(1),
                Value3 = GetSeq(2),
                Value4 = GetSeq(3),
                Value5 = GetSeq(4),
                Value6 = GetSeq(5),
                Value7 = GetSeq(6),
                Value8 = GetSeq(7)
            });

            list.Add(new ErrorData()
            {
                Name = "에러코드",
                Value = GetErrorCode(0),
                Value2 = GetErrorCode(1),
                Value3 = GetErrorCode(2),
                Value4 = GetErrorCode(3),
                Value5 = GetErrorCode(4),
                Value6 = GetErrorCode(5),
                Value7 = GetErrorCode(6),
                Value8 = GetErrorCode(7)
            });

            list.Add(new ErrorData()
            {
                Name = "에러명",
                Value = GetErrorName(0),
                Value2 = GetErrorName(1),
                Value3 = GetErrorName(2),
                Value4 = GetErrorName(3),
                Value5 = GetErrorName(4),
                Value6 = GetErrorName(5),
                Value7 = GetErrorName(6),
                Value8 = GetErrorName(7)
            });

            list.Add(new ErrorData()
            {
                Name = "온수 Temp",
                Value = GetHotTemp(0),
                Value2 = GetHotTemp(1),
                Value3 = GetHotTemp(2),
                Value4 = GetHotTemp(3),
                Value5 = GetHotTemp(4),
                Value6 = GetHotTemp(5),
                Value7 = GetHotTemp(6),
                Value8 = GetHotTemp(7)
            });

            list.Add(new ErrorData()
            {
                Name = "냉수 Temp",
                Value = GetColdTemp(0),
                Value2 = GetColdTemp(1),
                Value3 = GetColdTemp(2),
                Value4 = GetColdTemp(3),
                Value5 = GetColdTemp(4),
                Value6 = GetColdTemp(5),
                Value7 = GetColdTemp(6),
                Value8 = GetColdTemp(7)
            });

            list.Add(new ErrorData()
            {
                Name = "ADC HOT",
                Value = GetAdcHot(0),
                Value2 = GetAdcHot(1),
                Value3 = GetAdcHot(2),
                Value4 = GetAdcHot(3),
                Value5 = GetAdcHot(4),
                Value6 = GetAdcHot(5),
                Value7 = GetAdcHot(6),
                Value8 = GetAdcHot(7)
            });

            list.Add(new ErrorData()
            {
                Name = "ADC COLD",
                Value = GetAdcCold(0),
                Value2 = GetAdcCold(1),
                Value3 = GetAdcCold(2),
                Value4 = GetAdcCold(3),
                Value5 = GetAdcCold(4),
                Value6 = GetAdcCold(5),
                Value7 = GetAdcCold(6),
                Value8 = GetAdcCold(7)
            });

            list.Add(new ErrorData()
            {
                Name = "초기급수 완료",
                Value = GetWaterInitDone(0),
                Value2 = GetWaterInitDone(1),
                Value3 = GetWaterInitDone(2),
                Value4 = GetWaterInitDone(3),
                Value5 = GetWaterInitDone(4),
                Value6 = GetWaterInitDone(5),
                Value7 = GetWaterInitDone(6),
                Value8 = GetWaterInitDone(7)
            });

            list.Add(new ErrorData()
            {
                Name = "StatusA Raw",
                Value = GetStatusARaw(0),
                Value2 = GetStatusARaw(1),
                Value3 = GetStatusARaw(2),
                Value4 = GetStatusARaw(3),
                Value5 = GetStatusARaw(4),
                Value6 = GetStatusARaw(5),
                Value7 = GetStatusARaw(6),
                Value8 = GetStatusARaw(7)
            });

            list.Add(new ErrorData()
            {
                Name = "StatusB Raw",
                Value = GetStatusBRaw(0),
                Value2 = GetStatusBRaw(1),
                Value3 = GetStatusBRaw(2),
                Value4 = GetStatusBRaw(3),
                Value5 = GetStatusBRaw(4),
                Value6 = GetStatusBRaw(5),
                Value7 = GetStatusBRaw(6),
                Value8 = GetStatusBRaw(7)
            });

            list.Add(new ErrorData() { Name = "A Heater", Value = GetStatusABit(0, 0x01), Value2 = GetStatusABit(1, 0x01), Value3 = GetStatusABit(2, 0x01), Value4 = GetStatusABit(3, 0x01), Value5 = GetStatusABit(4, 0x01), Value6 = GetStatusABit(5, 0x01), Value7 = GetStatusABit(6, 0x01), Value8 = GetStatusABit(7, 0x01) });
            list.Add(new ErrorData() { Name = "A Compressor", Value = GetStatusABit(0, 0x02), Value2 = GetStatusABit(1, 0x02), Value3 = GetStatusABit(2, 0x02), Value4 = GetStatusABit(3, 0x02), Value5 = GetStatusABit(4, 0x02), Value6 = GetStatusABit(5, 0x02), Value7 = GetStatusABit(6, 0x02), Value8 = GetStatusABit(7, 0x02) });
            list.Add(new ErrorData() { Name = "A HotValve", Value = GetStatusABit(0, 0x04), Value2 = GetStatusABit(1, 0x04), Value3 = GetStatusABit(2, 0x04), Value4 = GetStatusABit(3, 0x04), Value5 = GetStatusABit(4, 0x04), Value6 = GetStatusABit(5, 0x04), Value7 = GetStatusABit(6, 0x04), Value8 = GetStatusABit(7, 0x04) });
            list.Add(new ErrorData() { Name = "A ColdSelect", Value = GetStatusABit(0, 0x08), Value2 = GetStatusABit(1, 0x08), Value3 = GetStatusABit(2, 0x08), Value4 = GetStatusABit(3, 0x08), Value5 = GetStatusABit(4, 0x08), Value6 = GetStatusABit(5, 0x08), Value7 = GetStatusABit(6, 0x08), Value8 = GetStatusABit(7, 0x08) });
            list.Add(new ErrorData() { Name = "A OutletValve", Value = GetStatusABit(0, 0x10), Value2 = GetStatusABit(1, 0x10), Value3 = GetStatusABit(2, 0x10), Value4 = GetStatusABit(3, 0x10), Value5 = GetStatusABit(4, 0x10), Value6 = GetStatusABit(5, 0x10), Value7 = GetStatusABit(6, 0x10), Value8 = GetStatusABit(7, 0x10) });
            list.Add(new ErrorData() { Name = "A PumpOutlet", Value = GetStatusABit(0, 0x20), Value2 = GetStatusABit(1, 0x20), Value3 = GetStatusABit(2, 0x20), Value4 = GetStatusABit(3, 0x20), Value5 = GetStatusABit(4, 0x20), Value6 = GetStatusABit(5, 0x20), Value7 = GetStatusABit(6, 0x20), Value8 = GetStatusABit(7, 0x20) });
            list.Add(new ErrorData() { Name = "A PumpDiaphragm", Value = GetStatusABit(0, 0x40), Value2 = GetStatusABit(1, 0x40), Value3 = GetStatusABit(2, 0x40), Value4 = GetStatusABit(3, 0x40), Value5 = GetStatusABit(4, 0x40), Value6 = GetStatusABit(5, 0x40), Value7 = GetStatusABit(6, 0x40), Value8 = GetStatusABit(7, 0x40) });
            list.Add(new ErrorData() { Name = "A PumpAirvent", Value = GetStatusABit(0, 0x80), Value2 = GetStatusABit(1, 0x80), Value3 = GetStatusABit(2, 0x80), Value4 = GetStatusABit(3, 0x80), Value5 = GetStatusABit(4, 0x80), Value6 = GetStatusABit(5, 0x80), Value7 = GetStatusABit(6, 0x80), Value8 = GetStatusABit(7, 0x80) });

            list.Add(new ErrorData() { Name = "B FloatSensor", Value = GetStatusBBit(0, 0x01), Value2 = GetStatusBBit(1, 0x01), Value3 = GetStatusBBit(2, 0x01), Value4 = GetStatusBBit(3, 0x01), Value5 = GetStatusBBit(4, 0x01), Value6 = GetStatusBBit(5, 0x01), Value7 = GetStatusBBit(6, 0x01), Value8 = GetStatusBBit(7, 0x01) });
            list.Add(new ErrorData() { Name = "B BallTop", Value = GetStatusBBit(0, 0x02), Value2 = GetStatusBBit(1, 0x02), Value3 = GetStatusBBit(2, 0x02), Value4 = GetStatusBBit(3, 0x02), Value5 = GetStatusBBit(4, 0x02), Value6 = GetStatusBBit(5, 0x02), Value7 = GetStatusBBit(6, 0x02), Value8 = GetStatusBBit(7, 0x02) });
            list.Add(new ErrorData() { Name = "B WaterBuffer", Value = GetStatusBBit(0, 0x04), Value2 = GetStatusBBit(1, 0x04), Value3 = GetStatusBBit(2, 0x04), Value4 = GetStatusBBit(3, 0x04), Value5 = GetStatusBBit(4, 0x04), Value6 = GetStatusBBit(5, 0x04), Value7 = GetStatusBBit(6, 0x04), Value8 = GetStatusBBit(7, 0x04) });
            list.Add(new ErrorData() { Name = "B EmptyDetect", Value = GetStatusBBit(0, 0x08), Value2 = GetStatusBBit(1, 0x08), Value3 = GetStatusBBit(2, 0x08), Value4 = GetStatusBBit(3, 0x08), Value5 = GetStatusBBit(4, 0x08), Value6 = GetStatusBBit(5, 0x08), Value7 = GetStatusBBit(6, 0x08), Value8 = GetStatusBBit(7, 0x08) });
            list.Add(new ErrorData() { Name = "B BufferLow", Value = GetStatusBBit(0, 0x10), Value2 = GetStatusBBit(1, 0x10), Value3 = GetStatusBBit(2, 0x10), Value4 = GetStatusBBit(3, 0x10), Value5 = GetStatusBBit(4, 0x10), Value6 = GetStatusBBit(5, 0x10), Value7 = GetStatusBBit(6, 0x10), Value8 = GetStatusBBit(7, 0x10) });
            list.Add(new ErrorData() { Name = "B Reheat Running", Value = GetStatusBBit(0, 0x20), Value2 = GetStatusBBit(1, 0x20), Value3 = GetStatusBBit(2, 0x20), Value4 = GetStatusBBit(3, 0x20), Value5 = GetStatusBBit(4, 0x20), Value6 = GetStatusBBit(5, 0x20), Value7 = GetStatusBBit(6, 0x20), Value8 = GetStatusBBit(7, 0x20) });
            list.Add(new ErrorData() { Name = "B HotIng", Value = GetStatusBBit(0, 0x40), Value2 = GetStatusBBit(1, 0x40), Value3 = GetStatusBBit(2, 0x40), Value4 = GetStatusBBit(3, 0x40), Value5 = GetStatusBBit(4, 0x40), Value6 = GetStatusBBit(5, 0x40), Value7 = GetStatusBBit(6, 0x40), Value8 = GetStatusBBit(7, 0x40) });
            list.Add(new ErrorData() { Name = "B Dispensing", Value = GetStatusBBit(0, 0x80), Value2 = GetStatusBBit(1, 0x80), Value3 = GetStatusBBit(2, 0x80), Value4 = GetStatusBBit(3, 0x80), Value5 = GetStatusBBit(4, 0x80), Value6 = GetStatusBBit(5, 0x80), Value7 = GetStatusBBit(6, 0x80), Value8 = GetStatusBBit(7, 0x80) });

            list.Add(new ErrorData()
            {
                Name = "버퍼수위 부족",
                Value = GetBufferLow(0),
                Value2 = GetBufferLow(1),
                Value3 = GetBufferLow(2),
                Value4 = GetBufferLow(3),
                Value5 = GetBufferLow(4),
                Value6 = GetBufferLow(5),
                Value7 = GetBufferLow(6),
                Value8 = GetBufferLow(7)
            });

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ErrorGrid.ItemsSource = list;
                AutoSaveErrorDataToFileDebounced();
                _isReadingError = false;

                if (ReadErrorButton != null)
                    ReadErrorButton.IsEnabled = true;
            }));
        }

        // 경로에 UTF-8로 저장. IOException 발생 시 1회 재시도(50ms 대기)
        private static void WriteFileWithRetry(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(50);
                File.WriteAllText(path, content, Encoding.UTF8);
            }
        }

        // 기본 파일명(yyyyMMdd_HHmmss) 사용, 동일 초에 다중 저장 시 _clickNN 접미사로 충돌 방지
        private static string BuildUniqueErrorFilePath(string directory)
        {
            string baseName = "ErrorData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string full = System.IO.Path.Combine(directory, baseName + ".config");
            int suffix = 1;
            while (File.Exists(full) && suffix <= 99)
            {
                full = System.IO.Path.Combine(directory, $"{baseName}_click{suffix:D2}.config");
                suffix++;
            }
            return full;
        }

        // ErrorData 보존 정책: 90일 초과 파일 삭제, 이어서 개수 상한(기본 10000) 초과 시 오래된 파일부터 정리
        private void EnforceErrorDataRetention(string directory, int retentionDays, int maxFiles)
        {
            try
            {
                if (!Directory.Exists(directory)) return;

                var all = new DirectoryInfo(directory)
                    .GetFiles("*.config")
                    .OrderBy(f => f.CreationTimeUtc)
                    .ToList();

                // 90일 초과 파일 삭제
                DateTime limit = DateTime.UtcNow.AddDays(-retentionDays);
                foreach (var f in all.Where(f => f.CreationTimeUtc < limit).ToList())
                {
                    try { f.Delete(); } catch { /* 무시 */ }
                }

                // 최신 목록 다시 로드 후 개수 상한 체크
                all = new DirectoryInfo(directory)
                    .GetFiles("*.config")
                    .OrderBy(f => f.CreationTimeUtc)
                    .ToList();

                if (all.Count > maxFiles)
                {
                    int toDelete = all.Count - maxFiles;
                    foreach (var f in all.Take(toDelete))
                    {
                        try { f.Delete(); } catch { /* 무시 */ }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn("EnforceErrorDataRetention 처리 중 문제", ex);
            }
        }

        // 자동 저장: ErrorGrid.ItemsSource를 XML로 저장 (파일명 충돌 방지 + 재시도 + 90일 보존)
        private void AutoSaveErrorDataToFile()
        {
            try
            {
                var src = ErrorGrid.ItemsSource as System.Collections.IEnumerable;
                if (src == null)
                {
                    log.Warn("AutoSaveErrorDataToFile: ErrorGrid.ItemsSource가 비어 있음");
                    return;
                }

                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // XML 조립
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<configuration>");
                sb.AppendLine("  <configSections>");
                sb.AppendLine("    <section name=\"Error\" type=\"BliMonitorTest.setting.SettingSection, BliMonitorTest, Version=1.0.0.9, Culture=neutral, PublicKeyToken=null\" />");
                sb.AppendLine("  </configSections>");
                sb.AppendLine("  <appSettings>");
                sb.AppendLine("    <clear />");
                sb.AppendLine("  </appSettings>");
                sb.AppendLine("  <Error>");

                int index = 1;
                foreach (var row in src)
                {
                    string name = "";
                    string v1 = "";
                    string v2 = "";
                    string v3 = "";
                    string v4 = "";
                    string v5 = "";

                    if (row is ErrorData ed)
                    {
                        name = ed.Name ?? "";
                        v1 = ed.Value ?? "";
                        v2 = ed.Value2 ?? "";
                        v3 = ed.Value3 ?? "";
                        v4 = ed.Value4 ?? "";
                        v5 = ed.Value5 ?? "";
                    }
                    else if (row is SettingData sd)
                    {
                        name = sd.Name ?? "";
                        v1 = sd.Value.ToString() ?? "";
                        v2 = sd.Value2.ToString() ?? "";
                        v3 = sd.Value3.ToString() ?? "";
                        v4 = sd.Value4.ToString() ?? "";
                        v5 = sd.Value5.ToString() ?? "";
                    }
                    else
                    {
                        continue;
                    }

                    v1 = NormalizeZeroForSave(v1);
                    v2 = NormalizeZeroForSave(v2);
                    v3 = NormalizeZeroForSave(v3);
                    v4 = NormalizeZeroForSave(v4);
                    v5 = NormalizeZeroForSave(v5);

                    name = System.Security.SecurityElement.Escape(name);
                    v1 = System.Security.SecurityElement.Escape(v1);
                    v2 = System.Security.SecurityElement.Escape(v2);
                    v3 = System.Security.SecurityElement.Escape(v3);
                    v4 = System.Security.SecurityElement.Escape(v4);
                    v5 = System.Security.SecurityElement.Escape(v5);

                    sb.AppendLine($"    <add Name=\"{name}\" Value1=\"{v1}\" Value2=\"{v2}\" Value3=\"{v3}\" Value4=\"{v4}\" Value5=\"{v5}\" Index=\"{index}\" />");
                    index++;
                }

                sb.AppendLine("  </Error>");
                sb.AppendLine("</configuration>");

                // 파일 경로 생성(충돌 방지)
                string full = BuildUniqueErrorFilePath(dir);

                // 저장(재시도 1회)
                WriteFileWithRetry(full, sb.ToString());

                // 리스트 갱신
                SetErrorList();

                // 보존 정책 실행(90일 + 개수 상한)
                EnforceErrorDataRetention(dir, retentionDays, maxFiles);

                // 에러 스냅샷 DB 큐 적재
                string fileOnly = System.IO.Path.GetFileNameWithoutExtension(full);
                //EnqueueErrorEventsFromGrid(fileNameForSnapshot: fileOnly, sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_MULTI, channelNo: _channelNoForDb);

                log.Info($"AutoSaveErrorDataToFile: 자동 저장 완료 → {full}");
            }
            catch (Exception ex)
            {
                log.Error("AutoSaveErrorDataToFile 실패", ex);
                //MessageBox.Show("에러 데이터를 자동 저장하는 중 문제가 발생했습니다.");
                ToastMessage.ToastService.AppToast.Show("에러 데이터를 자동 저장하는 중 문제가 발생했습니다.");
            }
        }

        private RangeEnabledObservableCollection<SettingData> getModeData()
        {
            RangeEnabledObservableCollection<SettingData> list = new RangeEnabledObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "ON TIME CW" });
            list.Add(new SettingData() { Name = "OFF TIME CW" });
            list.Add(new SettingData() { Name = "ON TIME CCW" });
            list.Add(new SettingData() { Name = "OFF TIME CCW" });
            list.Add(new SettingData() { Name = "HEATER TEMP" });
            list.Add(new SettingData() { Name = "HEATER OFF TIME" });
            list.Add(new SettingData() { Name = "VENTILE TEMP" });
            list.Add(new SettingData() { Name = "OPERATE TIME" });
            return list;
        }

        private RangeEnabledObservableCollection<SettingData> getMotorData()
        {
            RangeEnabledObservableCollection<SettingData> list = new RangeEnabledObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "이물질 감지 시간" });
            list.Add(new SettingData() { Name = "이물질 감지 전류" });
            list.Add(new SettingData() { Name = "이물질 감지 횟수" });
            list.Add(new SettingData() { Name = "과부화 감지 전류" });
            list.Add(new SettingData() { Name = "과부하 감지 횟수" });
            return list;
        }

        private RangeEnabledObservableCollection<SettingData> getHeaterData()
        {
            RangeEnabledObservableCollection<SettingData> list = new RangeEnabledObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "열풍 온도 Step1" });
            list.Add(new SettingData() { Name = "열풍 온도 Step2" });
            list.Add(new SettingData() { Name = "열풍 온도 Step3" });
            list.Add(new SettingData() { Name = "열풍 온도 Step4" });
            list.Add(new SettingData() { Name = "열풍 온도 Step5" });
            list.Add(new SettingData() { Name = "열풍 온도 Step6" });
            return list;
        }

        private RangeEnabledObservableCollection<SettingData> getFanData()
        {
            RangeEnabledObservableCollection<SettingData> list = new RangeEnabledObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "배기 FAN 대기 온도" });
            list.Add(new SettingData() { Name = "배기 FAN 운전 온도" });
            return list;
        }

        private RangeEnabledObservableCollection<SettingData> getHeaterData2()
        {
            RangeEnabledObservableCollection<SettingData> list = new RangeEnabledObservableCollection<SettingData>();
            list.Add(new SettingData() { Name = "열풍 FAN SPEED" });
            list.Add(new SettingData() { Name = "열풍 히터 온도" });
            return list;
        }

        private RangeEnabledObservableCollection<CompileData> getCompileData(int year, int month, int day, int ver)
        {
            RangeEnabledObservableCollection<CompileData> list = new RangeEnabledObservableCollection<CompileData>();
            list.Add(new CompileData { Year = year, Month = month, Day = day, Ver = ver });
            return list;
        }

        // ===== 안전 삭제 유틸리티 =====
        private static bool TryUnsetReadOnly(FileInfo info)
        {
            try
            {
                if (info.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    info.IsReadOnly = false;
                    info.Refresh();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool MoveToRecycleBinSafe(string path, out string error)
        {
            error = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    error = "파일 없음";
                    return false;
                }

                TryUnsetReadOnly(info);

                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                return true;
            }
            catch (IOException)
            {
                error = "파일 사용 중";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "권한 부족";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool HardDeleteSafe(string path, out string error)
        {
            error = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    error = "파일 없음";
                    return false;
                }
                TryUnsetReadOnly(info);
                info.Delete();
                return true;
            }
            catch (IOException)
            {
                error = "파일 사용 중";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "권한 부족";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ===== 일괄 삭제 코어 =====
        private void DeleteSelectedCore(System.Collections.IList selectedItems, bool hardDelete, string kind)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                ToastMessage.ToastService.AppToast.Show("선택된 항목이 없습니다.");
                return;
            }

            var modeText = hardDelete ? "영구 삭제" : "휴지통으로 이동";
            if (MessageBox.Show($"{selectedItems.Count}개를 {modeText} 하시겠습니까?",
                                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            // 삭제 전 선택/스크롤 상태 백업
            PreserveSelectionAndScroll(kind == "Error" ? ErrorFileList : FileList,
                                       out var selected, out var firstIndex);

            var names = selectedItems.Cast<object>().Select(o => o.ToString()).ToList();
            int ok = 0, fail = 0;
            int inUse = 0, noPerm = 0, notFound = 0, unknown = 0;

            foreach (var name in names)
            {
                string dir = (kind == "Error")
                    ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData")
                    : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParameterSetting");

                string full = System.IO.Path.Combine(dir, name + ".config");

                bool result;
                string err;

                if (hardDelete)
                {
                    result = HardDeleteSafe(full, out err);
                }
                else
                {
                    result = MoveToRecycleBinSafe(full, out err);
                    if (!result && _allowHardDeleteFallback && err != "파일 사용 중")
                    {
                        // 열려있는 파일은 폴백하지 않음
                        result = HardDeleteSafe(full, out err);
                    }
                }

                if (result) ok++;
                else
                {
                    fail++;
                    switch (err)
                    {
                        case "파일 사용 중": inUse++; break;
                        case "권한 부족": noPerm++; break;
                        case "파일 없음": notFound++; break;
                        default: unknown++; break;
                    }
                    log.Warn($"삭제 실패 [{name}] → {err}");
                }
            }

            // 목록 갱신
            if (kind == "Error") SetErrorList(); else SetList();

            // 삭제 후 선택/스크롤 복원
            RestoreSelectionAndScroll(kind == "Error" ? ErrorFileList : FileList,
                                      selected, firstIndex);

            // 요약 메시지
            var sb = new StringBuilder();
            sb.Append($"{ok}개 삭제");
            if (fail > 0) sb.Append($", 실패 {fail}개");
            if (inUse > 0) sb.Append($" (열림 {inUse})");
            if (noPerm > 0) sb.Append($" (권한 {noPerm})");
            if (notFound > 0) sb.Append($" (없음 {notFound})");
            ToastMessage.ToastService.AppToast.Show(sb.ToString(), 3000);
        }

        // ===== 리스트 선택/스크롤 상태 보존 유틸 =====
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void PreserveSelectionAndScroll(ListBox listBox, out List<string> selected, out int firstIndex)
        {
            selected = listBox.SelectedItems.Cast<object>().Select(o => o.ToString()).ToList();
            int first = 0;
            var sv = FindVisualChild<ScrollViewer>(listBox);
            if (sv != null) first = (int)sv.VerticalOffset;
            firstIndex = first;
        }

        private void RestoreSelectionAndScroll(ListBox listBox, List<string> selected, int firstIndex)
        {
            listBox.UpdateLayout();
            listBox.SelectedItems.Clear();
            foreach (var s in selected)
            {
                var item = listBox.Items.Cast<object>().FirstOrDefault(o => o.ToString() == s);
                if (item != null) listBox.SelectedItems.Add(item);
            }
            var sv = FindVisualChild<ScrollViewer>(listBox);
            if (sv != null) sv.ScrollToVerticalOffset(firstIndex);
        }

        private void AutoSaveErrorDataToFileDebounced()
        {
            _autoSaveCts?.Cancel();
            _autoSaveCts = new System.Threading.CancellationTokenSource();
            var token = _autoSaveCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_autoSaveDebounce, token);
                    if (token.IsCancellationRequested) return;

                    Dispatcher.Invoke(() => AutoSaveErrorDataToFile());
                }
                catch (TaskCanceledException) { /* 무시 */ }
            });
        }

        private void InitCollectionViews()
        {
            // ItemsSource 설정 후에 호출되어야 합니다.
            _fileView = CollectionViewSource.GetDefaultView(FileList.ItemsSource);
            _errorView = CollectionViewSource.GetDefaultView(ErrorFileList.ItemsSource);
        }

        private void ApplyFileFilter(string keyword)
        {
            if (_fileView == null) _fileView = CollectionViewSource.GetDefaultView(FileList.ItemsSource);
            if (_fileView == null) return;

            _fileView.Filter = o =>
            {
                var s = o?.ToString() ?? string.Empty;
                return s.IndexOf(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            _fileView.Refresh();
        }

        private void ApplyErrorFilter(string keyword)
        {
            if (_errorView == null) _errorView = CollectionViewSource.GetDefaultView(ErrorFileList.ItemsSource);
            if (_errorView == null) return;

            _errorView.Filter = o =>
            {
                var s = o?.ToString() ?? string.Empty;
                return s.IndexOf(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            _errorView.Refresh();
        }

        private void FileSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            ApplyFileFilter(tb?.Text);
        }

        private void ErrorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            ApplyErrorFilter(tb?.Text);
        }

        private static int TryInt0(string s)
        {
            if (int.TryParse((s ?? "").Trim(), out int v)) return v;
            return 0;
        }

        // 문자열 → double (실패/빈칸은 0)
        private static double TryDouble0(string s)
        {
            if (double.TryParse((s ?? "").Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v)) return v;
            if (double.TryParse((s ?? "").Trim(), out v)) return v;
            return 0;
        }

        /*
        private void OpenErrorDataQueryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 팝업 인스턴스 생성 (리소스 파싱 시점)
                var win = new ErrorDataQueryWindow
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // 파일명 필터 프리필(선택): ErrorFileName 텍스트가 있으면 전달
                string fileLike = (ErrorFileName?.Text ?? string.Empty).Trim();
                if (fileLike.Length == 0) fileLike = null;

                // 초기 필터 세팅: 단일채널(SOURCE_SINGLE), 현재 채널 번호(_channelNoForDb)
                // SetInitialFilter 시그니처: (int sourceType, int channelNo, string fileNameLike)
                win.SetInitialFilter(
                    sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE,
                    channelNo: _channelNoForDb,
                    fileNameLike: fileLike
                );

                // 마지막에 Show
                win.Show();
            }
            catch (Exception ex)
            {
                log.Warn("OpenErrorDataQueryButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("에러데이터 조회 화면을 여는 중 문제가 발생했습니다.");
            }
        }
        */

    }
}
