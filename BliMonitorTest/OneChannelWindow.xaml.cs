using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.server;
using BliMonitorTest.util;
using log4net;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FontWeights = System.Windows.FontWeights;
using Timer = System.Timers.Timer;

// Dummy Serial Port Namespace  
using DummySerialPortNs;
using System.Collections;

namespace BliMonitorTest
{
    public partial class OneChannelWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(OneChannelWindow));

        private Dictionary<int, int> seriesList = new Dictionary<int, int>();
        private SerialPort port;
        public bool Modify = false;

        private List<byte> receivedData = new List<byte>();
        public int parameterCnt = 0;
        private List<byte> parameterReceived = new List<byte>();
        private Timer timer;
        private TimeSpan TestTime;
        private Timer testTimer;
        private List<OxyColor> colorList = new List<OxyColor>();
        
        // 더미 포트(응답 생성 전용)
        private DummySerialPortNs.DummySerialPort _dummyPort;    // 더미 포트
        // 더미 모드 스위치: 체크박스 상태를 즉시 반영하는 계산 프로퍼티
        private bool UseDummy => UseDummyCheck?.IsChecked == true;
        // 현재 선택된 시뮬레이션 프로토콜
        private ProtocolKind CurrentKind = ProtocolKind.StartStopStatus;

        // 1) 더미 인터페이스 콤보값(1~5) → enum 매핑
        private void ApplyDummyKindFromCombo()
        {
            try
            {
                if (DummyInterfaceCombo?.SelectedItem is ComboBoxItem item)
                {
                    var selText = (item.Content?.ToString() ?? "").Trim();
                    if (!int.TryParse(selText, out var sel))
                        sel = 2;

                    switch (sel)
                    {
                        case 1: CurrentKind = ProtocolKind.ErrorDataRequest;    break;
                        case 2: CurrentKind = ProtocolKind.ErrorReset;          break;
                        case 3: CurrentKind = ProtocolKind.StartStopStatus;     break;
                        case 4: CurrentKind = ProtocolKind.ParameterRequest;    break;
                        case 5: CurrentKind = ProtocolKind.ParameterSet;        break; // 필요시 별도 매핑
                        default: CurrentKind = ProtocolKind.StartStopStatus;    break;
                    }
                }
            }
            catch
            {
                CurrentKind = ProtocolKind.StartStopStatus;
            }
        }

        public OneChannelWindow()
        {
            InitializeComponent();
            setItems(channel);
            FileName.TextChanged += FileName_TextChanged;
            Loaded += OneChannelWindow_Loaded;
            port = new SerialPort();
            port.BaudRate = 9600;
            port.DataReceived += Port_DataReceived;
            PortList.box.ItemsSource = SerialPort.GetPortNames();
            ConnectButton.Click += ConnectButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            channel.port = port;
            channel.OnTestStart += OnStart;
            channel.OnParameterLoadAction += Channel_OnParameterLoadAction;
            channel.OnCheckChanged += Channel_OnCheckChanged;
            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
            SaveCheck.Checked += SaveCheck_Checked;
            SaveCheck.Unchecked += SaveCheck_Unchecked;
            TestTime = TimeSpan.Zero;
            channel.chartView = Chart;
            colorList.Add(OxyColor.FromRgb(255, 0, 0));
            colorList.Add(OxyColor.FromRgb(0, 0, 255));
            colorList.Add(OxyColor.FromRgb(246, 190, 7));
            colorList.Add(OxyColor.FromRgb(7, 200, 246));
            colorList.Add(OxyColor.FromRgb(255, 0, 255));
            colorList.Add(OxyColor.FromRgb(1, 249, 125));
            colorList.Add(OxyColor.FromRgb(14, 128, 71));
            colorList.Add(OxyColor.FromRgb(0, 0, 0));
        }

        private void Channel_OnCheckChanged()
        {
            if (parameterReceived == null)
                parameterReceived = new List<byte>();
            parameterReceived.Clear();
            if (receivedData == null)
                receivedData = new List<byte>();
            receivedData.Clear();
            if (channel.Item3Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item3Check, new object[0]);
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item3Check, new object[0]);
            }
            if (channel.IsNewVersion)
            {
                channel.Item3.label.Content = "평균히터오프타임";
            }
            else
            {
                channel.Item3.label.Content = "열풍히터온도";
            }
        }

        private void Channel_OnParameterLoadAction()
        {
            if (parameterReceived == null)
                parameterReceived = new List<byte>();

            parameterReceived.Clear();
            parameterCnt = 1;
        }

        private void SaveCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            channel.SaveInDesktop = false;
        }

        private void SaveCheck_Checked(object sender, RoutedEventArgs e)
        {
            channel.SaveInDesktop = true;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            PortList.box.ItemsSource = SerialPort.GetPortNames();
            PortList.box.SelectedIndex = -1;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (port.IsOpen)
            {
                TestTime += TimeSpan.FromSeconds(1);

                log.Debug("onchannelwindow Timer_Elapsed sender : " + sender);

                log.Debug("onchannelwindow Timer_Elapsed parameterCnt : " + parameterCnt);
                log.Debug("onchannelwindow Timer_Elapsed TestTime : " + TestTime);
                log.Debug("onchannelwindow Timer_Elapsed channel.IsNewVersion : " + channel.IsNewVersion);

                //if (!channel.ParameterMode)
                if (parameterCnt > 0)
                {
                    Console.WriteLine("ParameterLoad");
                    receivedData.Clear();
                    parameterCnt--;
                    TestTime += TimeSpan.FromSeconds(1);
                }
                else
                {
                    if (channel.IsNewVersion)
                    {
                        //byte[] command = Protocol.GetNewCommand(1);
                        byte[] command = Protocol.GetNewCommand(1);
                        port.Write(command, 0, command.Length);
                        command.PrintHex(1);
                    }
                    else
                    {
                        byte[] command = Protocol.GetCommand(1);
                        port.Write(command, 0, command.Length);
                        command.PrintHex(1);
                    }
                }

                //}
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if(parameterCnt > 0)
            {
                Thread.Sleep(110);
            }
            else
            {
                Thread.Sleep(60);
            }            
            int Length = port.BytesToRead;
            byte[] data = new byte[Length];
            port.Read(data, 0, Length);

            log.Debug("============        LOG DATA OneChannelWindow.cs [Port_DataReceived] RESPONSE START       ==================");
            if(Length == 57 || Length == 70)
            {
                log.Debug("Port_DataReceived sender : " + sender);
                log.Debug("Port_DataReceived length OK DATA : " + data.Length);

                try
                {
                    var sp = sender as SerialPort;
                    if (sp == null || !sp.IsOpen) return;

                    var len = sp.BytesToRead;
                    if (len <= 0) return;

                    var buf = new byte[len];
                    var n = sp.Read(buf, 0, len);
                    if (n > 0)
                    {
                        // 실물은 프로토콜 프레이밍이 필요하다면 누적 후 완프레임에서 호출
                        receiveData(buf, n);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("RX error: " + ex);
                }
            }

            ByteLogHelper.LogPacket(data, "RX");
            ByteLogHelper.ToHexWith0x(data);
            ByteLogHelper.DumpLinesWith0x(data, 16);
            log.Debug("============        LOG DATA OneChannelWindow.cs [Port_DataReceived] RESPONSE END       ==================");

            //data.PrintHex(1);
            //receiveData(data, Length);
        }

        private void CheckNewDataValid(byte[] array)
        {
            int stx_cnt = 0;
            int etx_cnt = 0;
            List<int> STXIndex = new List<int>();
            List<int> ETXIndex = new List<int>();
            if (array.Length < 4)
            {
                return;
            }
            if (array[3] == array.Length)
            {
                CheckCommand(array);
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] == 0x12)
                    {
                        STXIndex.Add(i);
                        stx_cnt++;
                    }
                    else if (array[i] == 0x34)
                    {
                        ETXIndex.Add(i);
                        etx_cnt++;
                    }
                }
                if (stx_cnt > 1)
                {
                    if (stx_cnt == etx_cnt)
                    {
                        for (int i = 0; i < STXIndex.Count; i++)
                        {
                            ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i] - STXIndex[i] + 1);
                            if (command[3] == command.Length)
                            {
                                CheckCommand(command.ToArray());
                            }
                            else
                            {
                                
                            }
                            command.ToArray().PrintHex(1);
                        }
                    }
                    else
                    {
                        
                        if (etx_cnt > stx_cnt)
                        {
                            for (int i = 0; i < STXIndex.Count; i++)
                            {
                                ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i + 1] - STXIndex[i] + 1);
                                command.ToArray().PrintHex(1);
                                CheckCommand(command.ToArray());
                            }
                        }
                        else
                        {
                            for (int i = 0; i < ETXIndex.Count; i++)
                            {
                                ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i] - STXIndex[i] + 1);
                                command.ToArray().PrintHex(1);
                                CheckCommand(command.ToArray());
                            }
                        }
                    }
                }
            }
        }

        private void CheckDataValid(byte[] array)
        {
            int stx_cnt = 0;
            int etx_cnt = 0;
            List<int> STXIndex = new List<int>();
            List<int> ETXIndex = new List<int>();
            //array.PrintHex();
            if(array.Length < 3)
            {
                return;
            }
            if(array[3] == array.Length)
            {
                CheckCommand(array);
            }
            else
            {
                for(int i = 0; i < array.Length; i++)
                {
                    if(array[i] == 0xCC)
                    {
                        STXIndex.Add(i);
                        stx_cnt++;
                    }else if(array[i] == 0xEF)
                    {
                        ETXIndex.Add(i);
                        etx_cnt++;
                    }
                }
                if(stx_cnt > 1)
                {
                    if(stx_cnt == etx_cnt)
                    {
                        for(int i = 0; i < STXIndex.Count; i++)
                        {
                            ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i] - STXIndex[i] + 1);
                            if(command[3] == command.Length)
                            {
                                CheckCommand(command.ToArray());
                            }
                            else
                            {
                                
                            }
                            command.ToArray().PrintHex(1);
                        }
                    }
                    else
                    {
                        
                        if (etx_cnt > stx_cnt)
                        {
                            for (int i = 0; i < STXIndex.Count; i++)
                            {
                                ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i + 1] - STXIndex[i] + 1);
                                command.ToArray().PrintHex(1);
                                CheckCommand(command.ToArray());
                            }
                        }
                        else
                        {
                            for (int i = 0; i < ETXIndex.Count; i++)
                            {
                                ArrayView<byte> command = new ArrayView<byte>(array, STXIndex[i], ETXIndex[i] - STXIndex[i] + 1);
                                command.ToArray().PrintHex(1);
                                CheckCommand(command.ToArray());
                            }
                        }
                    }
                }
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if ( PortList.box.SelectedIndex == -1 && !UseDummyCheck.IsChecked.Value)
            {
                MessageBox.Show("포트가 선택 되지 않았습니다.");
                return;
            }

            // 1) 더미 인터페이스 적용
            ApplyDummyKindFromCombo();

            // 2) SerialPort를 실제로 Open(더미/실물 공통)
            try
            {
                if (port == null)
                    port = new SerialPort();

                // 포트 이름/속도는 기존 UI에서 가져오세요.
                // 아래는 예시: PortList.box.SelectedItem.ToString() 등
                // port.PortName = "COM5"; // 하드코딩 테스트 시
                // BaudRate 등 기존 설정 유지
                if (port.BaudRate <= 0) port.BaudRate = 9600;
                
                if(PortList.box.SelectedItem == null)
                {
                    port.PortName = "COM1";
                } else
                {
                    port.PortName = PortList.box.SelectedItem.ToString();
                }

                // 중복 이벤트 방지
                port.DataReceived -= Port_DataReceived;
                port.DataReceived += Port_DataReceived;

                if (!port.IsOpen)
                    port.Open();

                // 여기서 UI 연결 상태 갱신(예: 라벨/아이콘/버튼 텍스트 등)
                // UpdateUiConnected(true);
            }
            catch (Exception ex)
            {
                // UI에 연결 실패 표시
                // UpdateUiConnected(false);
                Console.WriteLine("Open failed: " + ex);
                return;
            }

            //new SuperSocketServer().init();
            //return;
            // 3) 동작 분기
            if (UseDummy)
            {
                // 더미 응답을 만들어 '수신된 것처럼' 밀어넣기
                if (_dummyPort == null) _dummyPort = new DummySerialPortNs.DummySerialPort();
                if (!_dummyPort.IsOpen) _dummyPort.Open();

                // REQ 생성 및 로깅(선택)
                var req = _dummyPort.GetRequestPacket(CurrentKind);
                if (req.Length > 0)
                {
                    // 실제 포트에도 써서 TX 로그/후처리가 동일하게 보이게 할 수 있음(옵션)
                    // 포트가 실제로 연결 안돼도 Write는 예외 없이 통과할 수 있음(가상 포트/드라이버 상황)
                    try {
                        log.Debug("============        LOG DATA ConnectButton_Click [req] REQUEST START       ==================");
                        port.Write(req, 0, req.Length);
                        ByteLogHelper.LogPacket(req, "RX");
                        ByteLogHelper.ToHexWith0x(req);
                        ByteLogHelper.DumpLinesWith0x(req, 16);
                        log.Debug("============        LOG DATA ConnectButton_Click [req] REQUEST END       ==================");
                    } catch { /* 무시 */ }
                }

                // 더미에서 해당 요청의 RES 생성
                var simulated = GetSimulatedResponse(_dummyPort, CurrentKind);
                if (simulated?.Length > 0)
                {
                    // 핵심: receiveData에 직접 주입해서 "수신된 것처럼" 처리
                    receiveData(simulated, simulated.Length);

                    log.Debug("============        LOG DATA ConnectButton_Click [req] RESPONSE START       ==================");
                    port.Write(simulated, 0, simulated.Length);
                    ByteLogHelper.LogPacket(simulated, "TX");
                    ByteLogHelper.ToHexWith0x(simulated);
                    ByteLogHelper.DumpLinesWith0x(simulated, 16);
                    log.Debug("============        LOG DATA ConnectButton_Click [req] RESPONSE END       ==================");
                }
            }
            else
            {
                try
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                        if (channel.streamWriter != null)
                        {
                            channel.streamWriter.Close();
                        }
                        channel.ConnectState = 0;
                        ConnectButton.Content = "연결";
                        return;
                    }
                    if (PortList.box.SelectedIndex == -1)
                    {
                        MessageBox.Show("포트가 선택 되지 않았습니다.");
                    }
                    else
                    {
                        port.PortName = PortList.box.SelectedItem.ToString();
                        port.Open();
                        channel.ConnectState = 1;
                        ConnectButton.Content = "해제";
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine(ex.ToString());
                    MessageBox.Show("포트를 확인 하세요");
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.ToString());
                    ConnectButton.Content = "연결";
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    MessageBox.Show("문제가 있습니다.");
                }
            }
        }

        // 더미 응답 생성 헬퍼(더미 포트 내부 상수 RSP를 복제해서 반환)
        private byte[] GetSimulatedResponse(DummySerialPort dummy, ProtocolKind kind)
        {
            try
            {
                switch (kind)
                {
                    case ProtocolKind.ErrorDataRequest:
                        return (byte[])dummy.RSP_ErrorData.Clone();
                    case ProtocolKind.ParameterRequest:
                        return (byte[])dummy.RSP_ParameterRequest.Clone();
                    case ProtocolKind.ParameterSet:
                        
                    case ProtocolKind.StartStopStatus:
                        return (byte[])dummy.RSP_StartStopStatus.Clone();
                    default:
                        return (byte[])dummy.RSP_StartStopStatus.Clone();
                }
                //switch (kind)
                //{
                //    case ProtocolKind.ErrorDataRequest:
                //        return (byte[])dummy.RSP_ErrorData.Clone();
                //    case ProtocolKind.ParameterRequest:
                //    case ProtocolKind.ParameterSet:
                //        return (byte[])dummy.RSP_ParameterRequest.Clone();
                //    case ProtocolKind.StartStopStatus:
                //    default:
                //        return (byte[])dummy.RSP_StartStopStatus.Clone();
                //}
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        // 실물 요청 바이트 헬퍼(더미 REQ 상수를 재사용)
        private byte[] GetRealRequestByKind(ProtocolKind kind)
        {
            if (_dummyPort == null) _dummyPort = new DummySerialPort();
            switch (kind)
            {
                case ProtocolKind.ErrorDataRequest:
                    return (byte[])_dummyPort.REQ_ErrorData.Clone();
                case ProtocolKind.ParameterRequest:
                    return (byte[])_dummyPort.REQ_ParameterRequest.Clone();
                case ProtocolKind.ParameterSet:
                    return (byte[])_dummyPort.REQ_ParameterSet.Clone();
                case ProtocolKind.StartStopStatus:
                default:
                    return (byte[])_dummyPort.REQ_StartStopStatus.Clone();
            }
        }

        private void OneChannelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            channel.setHandler(Item1Check_Click);
            channel.ConnectState = 0;
            channel.ChannelView.cont.FontWeight = FontWeights.Black;
            FileName.Text = DateTime.Now.ToString("TEST_yy년MM월dd일HH시mm분ss초");
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            VersionText.Content = "ver: " + version;
            SaveCheck.IsChecked = true;
        }

        private void FileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            channel.FName = FileName.Text;
            channel.Modify = true;
        }

        private void setItems(OneChannelValueDetail item)
        {            
            item.Item1.label.Content = "히터 온도";
            item.Item2.label.Content = "배기 온도";
            item.Item3.label.Content = "열풍히터온도";
            item.Item4.label.Content = "메인모터운전";
            item.Item5.label.Content = "배기팬 풍량";
            item.Item6.label.Content = "운전 시간";

            item.Item11.label.Content = "히터오프타임";
            item.Item12.label.Content = "배기온도평균";
            item.Item13.label.Content = "열풍히터Duty";
            item.Item14.label.Content = "메인모터전류";
            item.Item15.label.Content = "열풍팬 풍량";
            item.Item16.label.Content = "만수 감지";

            item.VersionBox.label.Content = "Model";
            item.CompileBox.label.Content = "Compile";
            item.Statebox.label.Content = "상태";

        }

        private int getIndex()
        {
            for (int i = 0; i < 8; i++)
            {
                if (!seriesList.ContainsValue(i))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Item1Check_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as Control).Name)
            {
                case "Item1Check":
                    if (channel.Item1Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[10] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, "히터 온도");
                            //Chart.seriesList[index].ItemsSource = channel.list1;
                            try
                            {
                                //Chart.setAxis(Chart.seriesList[index], 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                        else
                        {
                            channel.Item1Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list1.Clear();
                        int index = seriesList[10];
                        seriesList.Remove(10);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item2Check":
                    if (channel.Item2Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[11] = index;
                            Chart.setLegend(index, "배기 온도");
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            //Chart.seriesList[index].ItemsSource = channel.list2;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            channel.Item2Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list2.Clear();
                        int index = seriesList[11];
                        seriesList.Remove(11);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item3Check":
                    if (channel.Item3Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[12] = index;
                            if (channel.IsNewVersion)
                            {
                                Chart.ViewModel.setSeries(index, 1, colorList[index]);
                                Chart.setLegend(index, "평균히터오프타임");
                            }
                            else
                            {
                                Chart.ViewModel.setSeries(index, 0, colorList[index]);
                                Chart.setLegend(index, "열풍히터온도");
                            }
                            
                            //Chart.seriesList[index].ItemsSource = channel.list3;
                            //Chart.setAxis(Chart.seriesList[index], 0);
                        }
                        else
                        {
                            channel.Item3Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list3.Clear();
                        int index = seriesList[12];
                        seriesList.Remove(12);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item4Check":
                    if (channel.Item4Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[13] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "메인모터운전");
                            //Chart.seriesList[index].ItemsSource = channel.list4;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            channel.Item4Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list4.Clear();
                        int index = seriesList[13];
                        seriesList.Remove(13);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item5Check":
                    if (channel.Item5Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[14] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "히터오프타임");
                            //Chart.seriesList[index].ItemsSource = channel.list5;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            channel.Item5Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list5.Clear();
                        int index = seriesList[14];
                        seriesList.Remove(14);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item6Check":
                    if (channel.Item6Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[15] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, "배기온도평균");
                            //Chart.seriesList[index].ItemsSource = channel.list6;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            channel.Item6Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list6.Clear();
                        int index = seriesList[15];
                        seriesList.Remove(15);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item7Check":
                    if (channel.Item7Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[16] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "열풍히터Duty");
                            //Chart.seriesList[index].ItemsSource = channel.list7;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            channel.Item7Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list7.Clear();
                        int index = seriesList[16];
                        seriesList.Remove(16);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item8Check":
                    if (channel.Item8Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[17] = index;
                            Chart.ViewModel.setSeries(index, 2, colorList[index]);
                            Chart.setLegend(index, "메인모터전류");
                            //Chart.seriesList[index].ItemsSource = channel.list8;
                            //Chart.setAxis(Chart.seriesList[index], 2);
                        }
                        else
                        {
                            channel.Item8Check.IsChecked = false;
                            MessageBox.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list8.Clear();
                        int index = seriesList[17];
                        seriesList.Remove(17);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
            }
        }
    }
}
