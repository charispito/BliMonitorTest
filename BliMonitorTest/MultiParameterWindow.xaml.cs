using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.setting;
using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace BliMonitorTest
{
    /// <summary>
    /// MultiParameterWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MultiParameterWindow : Window
    {
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
        private RangeEnabledObservableCollection<SettingData> fan1 = new RangeEnabledObservableCollection<SettingData>();
        private List<byte> receivedData = new List<byte>();
        private ConfigFileManagement management;
        private RangeEnabledObservableCollection<SettingData> heater1 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater2 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater11 = new RangeEnabledObservableCollection<SettingData>();
        private RangeEnabledObservableCollection<SettingData> heater12 = new RangeEnabledObservableCollection<SettingData>();
        public MultiParameterWindow()
        {
            InitializeComponent();
        }

        public MultiParameterWindow(ChannelItem channelItem)
        {
            InitializeComponent();
            management = new ConfigFileManagement();
            this.channelItem = channelItem;
            Initialize();
            SetList();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += MultiParameterWindow_Loaded;
        }

        private void MultiParameterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InvalidateVisual();
        }

        private void SetList()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
            DirectoryInfo info = new DirectoryInfo(path);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
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
                MessageBox.Show("값이 설정 되지 않았습니다.");
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
            ReadErrorButton.Click += (s, e) =>
            {
                byte[] command = Protocol.GetError(channelItem.IsNewVersion);
                command.PrintHex(1);
                channelItem.client.GetStream().Write(command, 0, command.Length);
                Errorset = true;
                channelItem.setParameter(1);
            };
            WriteParamButton.Click += WriteParamButton_Click;
            ResetErrorButton.Click += ResetErrorButton_Click;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetList();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("delete");
            if (FileList.SelectedIndex == -1)
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
            if (channelItem.IsNewVersion)
            {
                if (receivedData.Count == 0)//받은것이 없을때
                {
                    if (arr[0] == 0x12)//head
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
                                byte[] command = Protocol.GetParameter(channelItem.IsNewVersion);
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
            else
            {
                if (receivedData.Count == 0)//받은것이 없을때
                {
                    if (arr[0] == 0xCC)//head
                    {
                        if (arr.Length > 4 && arr.Length == arr[3] && arr[arr.Length - 1] == 0xEF)
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
                                byte[] command = Protocol.GetParameter(channelItem.IsNewVersion);
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
                        if (receivedData.Last() == 0xEF)
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

        }

        private void checkErrorData(byte[] arr)
        {
            if (channelItem.IsNewVersion)
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
                                byte[] command = Protocol.GetError(channelItem.IsNewVersion);
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
            else
            {
                if (receivedData.Count == 0)//받은것이 없을때
                {
                    if (arr[0] == 0xCC)//head
                    {
                        if (arr.Length > 4 && arr.Length == arr[3] && arr[arr.Length - 1] == 0xEF)
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
                                byte[] command = Protocol.GetError(channelItem.IsNewVersion);
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
                        if (receivedData.Last() == 0xEF)
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

        }

        private void ReadParamButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = Protocol.GetParameter(channelItem.IsNewVersion);
            command.PrintHex(1);
            channelItem.setParameter(0);
            channelItem.client.GetStream().Write(command, 0, command.Length);
            channelItem.client.GetStream().Flush();
            
        }

        private byte[] GetParameterSettingData()
        {
            byte[] command = new byte[70];
            command[0] = 0xCC;
            if (channelItem.IsNewVersion)
            {
                command[0] = 0x12;
            }
            command[1] = 0x00;
            if (channelItem.IsNewVersion)
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
            command[69] = 0xEF;
            if (channelItem.IsNewVersion)
            {
                command[69] = 0x34;
            }
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
            byte[] command = Protocol.GetParameter(channelItem.IsNewVersion);
            //channelItem.client.GetStream().Write(command, 0, command.Length);
        }

        private void ResetErrorButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] command = null;
            if (channelItem.IsNewVersion)
            {
                command = Protocol.GetErrorReset(true);
            }
            else
            {
                command = Protocol.GetErrorReset(false);
            }
            command.PrintHex(1);

            channelItem.client.GetStream().Write(command, 0, command.Length);
            channelItem.client.GetStream().Flush();
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

            // CC 00 B9 46
            // 00 00 00 00 00 00 00 00 00 00 error0
            // 00 40 01 17 03 19 00 00 00 00 error1
            // 80 00 00 22 53 23 00 00 00 01 error2
            // 00 02 01 80 49 2A 00 00 00 01 error3
            // 00 02 01 7C 4A 2E 00 00 00 01 error4
            // 00 00 00 00 00 00 00 00 00 00 error5
            // 00 00 00 02 99 EF
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
                ErrorGrid.ItemsSource = list;
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
