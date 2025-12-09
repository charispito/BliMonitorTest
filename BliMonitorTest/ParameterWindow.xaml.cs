using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.setting;
using BliMonitorTest.util;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
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

namespace BliMonitorTest
{
    /// <summary>
    /// ParameterWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ParameterWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ParameterWindow));

        private SerialPort port = null;
        private bool Errorset = false;
        private OneChannelValueDetail oneChannel;
        private List<string> files;
        private bool RightSet = false;
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
        private RangeEnabledObservableCollection<SettingData> fan1 = new RangeEnabledObservableCollection<SettingData>();
        private List<byte> receivedData = new List<byte>();
        private ConfigFileManagement management;
        private RangeEnabledObservableCollection<SettingData> heater1 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater2 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater11 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater12 = new RangeEnabledObservableCollection<SettingData>();

        public ParameterWindow(SerialPort port, OneChannelValueDetail detail)
        {
            InitializeComponent();
            this.port = port;
            oneChannel = detail;
            Initialize2();
            InitializeSetting();
            management = new ConfigFileManagement();
            SetList();
        }

        public ParameterWindow()
        {
            InitializeComponent();
            InitializeSetting();
            management = new ConfigFileManagement();
            SetList();
            Initialize2();

            //Initialize();
        }


        private void InitializeSetting()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

        }

        private void Initialize2()
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
            BliMonitorTest.ItemsSource = list;
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
            HeaterGrid.ItemsSource = getHeaterData();
            FanGrid.ItemsSource = getFanData();
            HeaterGrid2.ItemsSource = getHeaterData2();
            HeaterGridSetting.ItemsSource = heater11;
            FanGrid2.ItemsSource = fan1;
            HeaterGridSetting2.ItemsSource = heater12;
            ComplieGrid.ItemsSource = getCompileData(2022, 1, 1, 0);
            SaveButton.Click += SaveButton_Click;
            DeleteButton.Click += DeleteButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            if (port != null)
            {
                Loaded += ParameterWindow_Loaded1;
                Closed += ParameterWindow_Closed1; ;
                RightButton.Click += RightButton_Click;
                ReadParamButton.Click += ReadParamButton_Click;
                ReadErrorButton.Click += (s, e) =>
                {
                    byte[] command = Protocol.GetError(oneChannel.IsNewVersion);
                    command.PrintHex(1);
                    oneChannel.setParameter();
                    port.Write(command, 0, command.Length);

                    log.Debug("============        LOG DATA ParameterWindow.cs [ReadErrorButton] RESPONSE START       ==================");
                    log.Debug("ReadErrorButton IsNewVersion : " + oneChannel.IsNewVersion);
                    ByteLogHelper.LogPacket(command, "RX");
                    ByteLogHelper.ToHexWith0x(command);
                    ByteLogHelper.DumpLinesWith0x(command, 16);
                    log.Debug("============        LOG DATA ParameterWindow.cs [ReadErrorButton] RESPONSE END       ==================");
                };
                WriteParamButton.Click += WriteParamButton_Click;
                ResetErrorButton.Click += ResetErrorButton_Click;
            }

        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetList();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("delete");
            if(FileList.SelectedIndex == -1)
            {
                MessageBox.Show("파일이 선택 되지 않았습니다.");
            }
            else
            {
                string name = FileList.SelectedItem.ToString();
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
                path += $"\\{name}.config";
                Console.WriteLine(path);
                FileInfo info = new FileInfo(path);
                if (info.Exists)
                {
                    info.Delete();
                    SetList();
                }
            }
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
            foreach(SectionItem section in setting.heater1)
            {
                heater11.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
            foreach (SectionItem section in setting.heater2)
            {
                heater12.Add(new SettingData() { Name = section.Name, Value = section.Value1, Value2 = section.Value2 });
            }
        }



        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileName.Text.Length == 0)
            {
                MessageBox.Show("파일명을 입력 하세요");
                return;
            }
            else
            {
                SettingItem item = GetSettingSectionData();
                management.CreateConfig(FileName.Text.ToString(), item);
                SetList();
                MessageBox.Show("저장 되었습니다.");
            }
        }

        private void SetList()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
            DirectoryInfo info = new DirectoryInfo(path);
            if (files == null)
                files = new List<string>();
            files.Clear();
            foreach (FileInfo file in info.GetFiles())
            {
                files.Add(file.Name.Replace(".config", ""));
            }
            FileList.ItemsSource = files;
            FileList.Items.Refresh();
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

        private void WriteParamButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RightSet)
            {
                MessageBox.Show("값이 설정 되지 않았습니다.");
                return;
            }
            
            byte[] command = GetParameterSettingData();

            log.Debug("============        LOG DATA ParameterWindow.cs [WriteParamButton_Click] RESPONSE START       ==================");
            log.Debug("WriteParamButton_Click RightSet : " + RightSet);
            ByteLogHelper.LogPacket(command, "RX");
            ByteLogHelper.ToHexWith0x(command);
            ByteLogHelper.DumpLinesWith0x(command, 16);
            log.Debug("============        LOG DATA ParameterWindow.cs [WriteParamButton_Click] RESPONSE END       ==================");

            port.Write(command, 0, command.Length);
            //PrintCommand(GetParameterSettingData());
        }

        private void ParameterWindow_Closed1(object sender, EventArgs e)
        {
            oneChannel.ParameterMode = false;

        }

        private void ParameterWindow_Loaded1(object sender, RoutedEventArgs e)
        {
            oneChannel.ParameterMode = true;
            byte[] command = Protocol.GetParameter(oneChannel.IsNewVersion);
            port.Write(command, 0, command.Length);
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

        private void ResetErrorButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = null;

            if (oneChannel.IsNewVersion)
            {
                command = Protocol.GetErrorReset(true);
            }
            else
            {
                command = Protocol.GetErrorReset(false);
            }
            command.PrintHex(1);

            log.Debug("============        LOG DATA ParameterWindow.cs [ResetErrorButton_Click] RESPONSE START       ==================");
            log.Debug("ResetErrorButton_Click oneChannel.IsNewVersion : " + oneChannel.IsNewVersion);
            ByteLogHelper.LogPacket(command, "RX");
            ByteLogHelper.ToHexWith0x(command);
            ByteLogHelper.DumpLinesWith0x(command, 16);
            log.Debug("============        LOG DATA ParameterWindow.cs [ResetErrorButton_Click] RESPONSE END       ==================");

            if (port != null)
            {
                port.Write(command, 0, command.Length);
            }
        }

        private void ReadParamButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = Protocol.GetParameter(oneChannel.IsNewVersion);        // 0000  12 01 99 07 00 60 34
            command.PrintHex(1);

            log.Debug("============        LOG DATA ParameterWindow.cs [ReadParamButton_Click] RESPONSE START       ==================");
            log.Debug("ReadParamButton_Click oneChannel.IsNewVersion : " + oneChannel.IsNewVersion);
            ByteLogHelper.LogPacket(command, "RX");
            ByteLogHelper.ToHexWith0x(command);
            ByteLogHelper.DumpLinesWith0x(command, 16);
            log.Debug("============        LOG DATA ParameterWindow.cs [ReadParamButton_Click] RESPONSE END       ==================");

            if (port != null)
            {
                oneChannel.setParameter();
                //oneChannel.OnParameterLoadAction();
                port.Write(command, 0, command.Length);
            }
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
            heater11.Clear();
            heater12.Clear();
            mode11.AddRange(mode1);
            mode12.AddRange(mode2);
            mode13.AddRange(mode3);
            mode14.AddRange(mode4);
            mode15.AddRange(mode5);
            motor1.AddRange(motor);
            fan1.AddRange(fan);
            heater11.AddRange(heater1);
            heater12.AddRange(heater2);
            SetGrid1.ItemsSource = mode11;
            SetGrid2.ItemsSource = mode12;
            SetGrid3.ItemsSource = mode13;
            SetGrid4.ItemsSource = mode14;
            SetGrid5.ItemsSource = mode15;
            SetGrid6.ItemsSource = motor1;
            FanGrid2.ItemsSource = fan1;
            HeaterGridSetting.ItemsSource = heater11;
            HeaterGridSetting2.ItemsSource = heater12;
        }
        
        public void setParameter(byte[] data)
        {
            log.Debug("parameterwindow  data.Length 537 : " + data.Length);
            log.Debug("parameterwindow  data : " + data);

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
            

            int onTimeCW2 = data[14];
            int offTimeCW2 = data[15];
            int onTimeCCW2 = data[16];
            int offTimeCCW2 = data[17];
            int HeaterTemp2 = data[18];
            int HeaterOffTime2 = data[19];
            int VentileTemp2 = data[20];
            int OperateTime2 = data[21];
            int ExhaustFanOperateMode = data[22];

            

            int onTimeCW3 = data[24];
            int offTimeCW3 = data[25];
            int onTimeCCW3 = data[26];
            int offTimeCCW3 = data[27];
            int HeaterTemp3 = data[28];
            int HeaterOffTime3 = data[29];
            int VentileTemp3 = data[30];
            int OperateTime3 = data[31];

            

            int onTimeCW4 = data[34];
            int offTimeCW4 = data[35];
            int onTimeCCW4 = data[36];
            int offTimeCCW4 = data[37];
            int HeaterTemp4 = data[38];
            int HeaterOffTime4 = data[39];
            int VentileTemp4 = data[40];
            int OperateTime4 = data[41];

            

            int onTimeCW5 = data[44];
            int offTimeCW5 = data[45];
            int onTimeCCW5 = data[46];
            int offTimeCCW5 = data[47];
            int HeaterTemp5 = data[48];
            int HeaterOffTime5 = data[49];
            int VentileTemp5 = data[50];
            int OperateTime5 = data[51];

            

            int motor1 = data[54];
            int motor2 = data[55];
            int motor3 = data[56];
            int motor4 = data[57];
            int motor5 = data[58];

            int air_step1 = data[13];
            int air_step2 = data[23];
            int air_step3 = data[33];
            int air_step4 = data[43];
            int air_step5 = data[53];
            int air_fan_speed = data[59];
            int air_control1 = data[60];
            int air_control2 = data[61];
            int air_control3 = data[62];
            int air_control4 = data[63];
            int air_control5 = data[64];
            int air_heater_temp = data[65];
            int air_control6 = data[66];

            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (mode1 == null)
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

                if (heater1 == null)
                {
                    heater1 = new RangeEnabledObservableCollection<SettingData>();
                }
                heater1.Clear();
                heater1.Add(new SettingData() { Name = "열풍 온도 Step1", Value = air_control1, Value2 = air_step1 });
                heater1.Add(new SettingData() { Name = "열풍 온도 Step2", Value = air_control2, Value2 = air_step2 });
                heater1.Add(new SettingData() { Name = "열풍 온도 Step3", Value = air_control3, Value2 = air_step3 });
                heater1.Add(new SettingData() { Name = "열풍 온도 Step4", Value = air_control4, Value2 = air_step4 });
                heater1.Add(new SettingData() { Name = "열풍 온도 Step5", Value = air_control5, Value2 = air_step5 });
                heater1.Add(new SettingData() { Name = "열풍 온도 Step6", Value = air_control6, Value2 = 0 });

                if (heater2 == null)
                {
                    heater2 = new RangeEnabledObservableCollection<SettingData>();
                }
                heater2.Clear();
                heater2.Add(new SettingData() { Name = "열풍 FAN SPEED", Value = air_heater_temp });
                heater2.Add(new SettingData() { Name = "열풍 히터 온도", Value = air_fan_speed });

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
                HeaterGrid.ItemsSource = heater1;
                HeaterGrid2.ItemsSource = heater2;
            }));

        }

        private byte[] GetParameterSettingData()
        {
            byte[] command = new byte[70];
            command[0] = 0xCC;

            log.Debug(command);
            log.Debug(" parameterwindow command 773 :  " + command);    
            log.Debug(" parameterwindow command 773 :  " + command[0]);
            log.Debug(" parameterwindow command 773 :  " + command[1]);
            log.Debug(" parameterwindow command 773 :  " + command);


            if (oneChannel.IsNewVersion)
            {
                command[0] = 0x12;
            }
            command[1] = 0x00;
            if (oneChannel.IsNewVersion)
            {
                command[1] = 0x01;
            }
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
            //CC 01 96 46
            //28 1C 28 1C 7D 00 5F 04 28 01
            //32 05 32 05 7D 3A 5F 05 50 04
            //55 05 55 05 7D 3C 5F 06 00 05
            //46 02 46 02 7D 3E 5F 07 00 06
            //46 02 46 02 7D 00 5F 08 00 07
            //14 07 05 09 14 01 01 03 04 06 03 36 02
            //00 87 EF

            command[69] = 0xEF;
            if (oneChannel.IsNewVersion)
            {
                command[69] = 0x34;
            }
            return command;
        }
        public void setError(byte[] data)
        {
            //int motor01 = data[4];
            //int motor02 = data[5];
            int runmode0 = data[6] + 1;
            int heaterTemp0 = data[7];
            int heaterofftime0 = data[8];
            int exhaustTemp0 = data[9];
            int hotWindTemp0 = data[10];
            int hotWindOnTime0 = data[11];
            byte[] times1 = new byte[] { data[13], data[12] };
            int intTimes1 = BitConverter.ToInt16(times1, 0);

            Console.WriteLine("{0} {1} {2}", runmode0, heaterTemp0, heaterofftime0);

            int runmode1 = data[16] + 1;
            int heaterTemp1 = data[17];
            int heaterofftime1 = data[18];
            int exhaustTemp1 = data[19];
            int hotWindTemp1 = data[20];
            int hotWindOnTime1 = data[21];
            byte[] times2 = new byte[] { data[23], data[22] };
            int intTimes2 = BitConverter.ToInt16(times2, 0);


            int runmode2 = data[26] + 1;
            int heaterTemp2 = data[27];
            int heaterofftime2 = data[28];
            int exhaustTemp2 = data[29];
            int hotWindTemp2 = data[30];
            int hotWindOnTime2 = data[31];
            byte[] times3 = new byte[] { data[33], data[32] };
            int intTimes3 = BitConverter.ToInt16(times3, 0);


            int runmode3 = data[36] + 1;
            int heaterTemp3 = data[37];
            int heaterofftime3 = data[38];
            int exhaustTemp3 = data[39];
            int hotWindTemp3 = data[40];
            int hotWindOnTime3 = data[41];
            byte[] times4 = new byte[] { data[43], data[42] };
            int intTimes4 = BitConverter.ToInt16(times4, 0);


            int runmode4 = data[46] + 1;
            int heaterTemp4 = data[47];
            int heaterofftime4 = data[48];
            int exhaustTemp4 = data[49];
            int hotWindTemp4 = data[50];
            int hotWindOnTime4 = data[51];
            byte[] times5 = new byte[] { data[53], data[52] };
            int intTimes5 = BitConverter.ToInt16(times5, 0);

            byte[] times = new byte[] { data[67], data[66] };
            Console.WriteLine("First: {0} Second: {1}", data[66].ToString("X2"), data[67].ToString("X2"));
            int timesInt = BitConverter.ToInt16(times, 0);

            byte error0 = data[4];
            byte error1 = data[5];
            byte error2 = data[14];
            byte error3 = data[15];
            byte error4 = data[24];
            byte error5 = data[25];
            byte error6 = data[34];
            byte error7 = data[35];
            byte error8 = data[44];
            byte error9 = data[45];
            int hot_air_fan_duty = data[30];
            int[] binary0 = Enumerable.Range(1, 8).Select(i => error0 / (1 << (8 - i)) % 2).ToArray();
            int[] binary1 = Enumerable.Range(1, 8).Select(i => error1 / (1 << (8 - i)) % 2).ToArray();
            int[] binary2 = Enumerable.Range(1, 8).Select(i => error2 / (1 << (8 - i)) % 2).ToArray();
            int[] binary3 = Enumerable.Range(1, 8).Select(i => error3 / (1 << (8 - i)) % 2).ToArray();
            int[] binary4 = Enumerable.Range(1, 8).Select(i => error4 / (1 << (8 - i)) % 2).ToArray();
            int[] binary5 = Enumerable.Range(1, 8).Select(i => error5 / (1 << (8 - i)) % 2).ToArray();
            int[] binary6 = Enumerable.Range(1, 8).Select(i => error6 / (1 << (8 - i)) % 2).ToArray();
            int[] binary7 = Enumerable.Range(1, 8).Select(i => error7 / (1 << (8 - i)) % 2).ToArray();
            int[] binary8 = Enumerable.Range(1, 8).Select(i => error8 / (1 << (8 - i)) % 2).ToArray();
            int[] binary9 = Enumerable.Range(1, 8).Select(i => error9 / (1 << (8 - i)) % 2).ToArray();

            ObservableCollection<ErrorData> list = new ObservableCollection<ErrorData>();
            list.Add(new ErrorData() { Name = "에러 내용", Value = GetErrorName(binary0, binary1), Value2 = GetErrorName(binary2, binary3), Value3 = GetErrorName(binary4, binary5), Value4 = GetErrorName(binary6, binary7), Value5 = GetErrorName(binary8, binary9) });
            list.Add(new ErrorData() { Name = "운전 모드", Value = runmode0.ToString(), Value2 = runmode1.ToString(), Value3 = runmode2.ToString(), Value4 = runmode3.ToString(), Value5 = runmode4.ToString() });
            list.Add(new ErrorData() { Name = "히터 온도", Value = heaterTemp0.ToString(), Value2 = heaterTemp1.ToString(), Value3 = heaterTemp2.ToString(), Value4 = heaterTemp3.ToString(), Value5 = heaterTemp4.ToString() });
            list.Add(new ErrorData() { Name = "히터 오프 타임", Value = heaterofftime0.ToString(), Value2 = heaterofftime1.ToString(), Value3 = heaterofftime2.ToString(), Value4 = heaterofftime3.ToString(), Value5 = heaterofftime4.ToString() });
            list.Add(new ErrorData() { Name = "배기 온도", Value = exhaustTemp0.ToString(), Value2 = exhaustTemp1.ToString(), Value3 = exhaustTemp2.ToString(), Value4 = exhaustTemp3.ToString(), Value5 = exhaustTemp4.ToString() });
            list.Add(new ErrorData() { Name = "열풍 온도", Value = hotWindTemp0.ToString(), Value2 = hotWindTemp1.ToString(), Value3 = hotWindTemp2.ToString(), Value4 = hotWindTemp3.ToString(), Value5 = hotWindTemp4.ToString() });
            list.Add(new ErrorData() { Name = "열풍 On Time", Value = hotWindOnTime0.ToString(), Value2 = hotWindOnTime1.ToString(), Value3 = hotWindOnTime2.ToString(), Value4 = hotWindOnTime3.ToString(), Value5 = hotWindOnTime4.ToString() });
            list.Add(new ErrorData() { Name = "운전 횟수", Value = intTimes1.ToString(), Value2 = intTimes2.ToString(), Value3 = intTimes3.ToString(), Value4 = intTimes4.ToString(), Value5 = intTimes5.ToString() });
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BliMonitorTest.ItemsSource = list;
                RunCount.Content = timesInt;
            }));
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
    }
}
