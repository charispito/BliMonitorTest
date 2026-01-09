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
using BliMonitorTest.dummy;

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

        private volatile bool _useDummyCached;
        // UI 접근 없이 작업 스레드에서 안전하게 읽을 수 있는 프로퍼티
        private bool UseDummy => _useDummyCached;

        // 현재 선택된 시뮬레이션 프로토콜
        //private ProtocolKind CurrentKind = ProtocolKind.StartStopStatus;
        private byte[] _dummyLastBuffer;
        private readonly DummyValueGenerator _dummyGen = new DummyValueGenerator();

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
            _useDummyCached = (UseDummyCheck?.IsChecked == true);       // 초기 캐시 동기화 (UI 스레드)
            UseDummyCheck.Checked += UseDummyCheck_Checked;
            UseDummyCheck.Unchecked += UseDummyCheck_Unchecked;
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

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
                     // 타이머 스레드에서 예외로 멈추지 않도록 try-catch
            try
            {
                DoPeriodicTickCore();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void DoPeriodicTickCore()
        {
                     // ConnectState=1일 때만 수행
            if (channel.ConnectState != 1) return;

                     // 더미/실기구 스냅샷
            bool useDummy = UseDummy;

            try
            {
                if (!UseDummy)
                {
                    if (port.IsOpen)
                    {
                        TestTime += TimeSpan.FromSeconds(1);

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
                    }
                }
                else
                {
                                     //  더미: 동일 데이터 지속 응답 주입
                     // var rsp = GetSimulatedResponse(_dummyPort, ProtocolKind.StartStopStatus);
                                    // 상태 응답(57바이트) 프레임을 하나 만들고, 그 안에 더미 값을 심어서
                                    // 실기와 동일하게 receiveData(...) 경로로 흘려보낸다.
                    var rsp = GetSimulatedResponse(_dummyPort, DummySerialPortNs.ProtocolKind.StartStopStatus);

                    if (rsp != null && rsp.Length > 0)
                        if (rsp != null && rsp.Length >= 57)
                        {
                            var sample = _dummyGen.Next();

                                                 // 엑셀 정의서 기준 오프셋에 값 세팅 + 체크섬 갱신
                            DummyFramePatcher.PatchStatusResponse57(rsp, sample);

                            _dummyLastBuffer = rsp;
                            ByteLogHelper.LogPacket(rsp, "RX");
                            //ByteLogHelper.LogPacket(rsp, "RX(DUMMY)");

                                                  // 실기 수신과 동일 루트로 주입
                            InvokePortDataReceivedWith(rsp);
                        }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (UseDummy) return; // 더미는 Timer 경로에서 처리

            try
            {
                int toRead = port?.BytesToRead ?? 0;
                if (toRead <= 0) return;

                var tmp = new byte[toRead];
                int read = port.Read(tmp, 0, toRead);
                if (read <= 0) return;

                byte[] buf;
                if (read == toRead)
                {
                    buf = tmp;
                }
                else
                {
                    buf = new byte[read];
                    Array.Copy(tmp, 0, buf, 0, read);
                }

                Dispatcher.Invoke(() =>
                {
                    ByteLogHelper.LogPacket(buf, "RX");
                    receiveData(buf, buf.Length);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        // 실물 요청 바이트 헬퍼(더미 REQ 상수를 재사용)
        /*
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
        */

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
            if (array.Length < 3)
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
                    if (array[i] == 0xCC)
                    {
                        STXIndex.Add(i);
                        stx_cnt++;
                    }
                    else if (array[i] == 0xEF)
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

        private void PrepareIo()
        {
            // 실기 포트 준비
            if (port == null) port = new SerialPort();
            if (port.BaudRate <= 0) port.BaudRate = 9600;

            if (PortList.box.SelectedItem == null)
            {
                port.PortName = "";
            }
            else
            {
                port.PortName = PortList.box.SelectedItem.ToString();
            }

            if (!port.IsOpen)
            {
                try
                {
                    port.Open();
                    port.DataReceived -= Port_DataReceived;
                    port.DataReceived += Port_DataReceived;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Open failed: " + ex);
                }
            }

            // 더미 포트 준비
            if (_dummyPort == null) _dummyPort = new DummySerialPortNs.DummySerialPort();
            if (!_dummyPort.IsOpen) _dummyPort.Open();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 이미 연결되어 있으면 해제
                if (port != null && port.IsOpen)
                {
                    // 타이머 중지 및 핸들러 제거
                    StopTimerSafe();

                    // 수신 핸들러 해제
                    try { port.DataReceived -= Port_DataReceived; } catch { }

                    // 로그 스트림 닫기
                    if (channel.streamWriter != null)
                    {
                        try { channel.streamWriter.Close(); } catch { }
                        channel.streamWriter = null;
                    }

                    // 포트 닫기
                    try { port.Close(); } catch { }

                    // 상태/버튼
                    channel.ConnectState = 0;
                    ConnectButton.Content = "연결";
                    return;
                }

                // 포트 선택 확인
                if (PortList.box.SelectedIndex == -1)
                {
                    MessageBox.Show("포트가 선택 되지 않았습니다.");
                    return;
                }

                // 포트 열기 및 수신 핸들러 재등록(중복 제거 후 등록)
                port.PortName = PortList.box.SelectedItem.ToString();

                // 재연결 시 핸들러는 항상 ‘제거 후 등록’
                try { port.DataReceived -= Port_DataReceived; } catch { }
                port.Open();
                port.DataReceived += Port_DataReceived;

                // 상태/버튼
                channel.ConnectState = 1;
                ConnectButton.Content = "해제";

                // 타이머 확실히 시작(Elapsed 중복 제거 후 Start)
                StartTimerSafe();

                // 즉시 1회 수행(바로 동작 확인)
                DoPeriodicTickCore();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void InvokePortDataReceivedWith(byte[] buf)
        {
            if (buf == null || buf.Length == 0) return;
            // Port_DataReceived 단일 파이프라인 재사용
            // 더미에서는 실제 포트에서 읽지 않으므로, 그대로 receiveData 호출
            Dispatcher.Invoke(() =>
            {
                receiveData(buf, buf.Length);
            });

        }

        // 더미 응답 생성 헬퍼(더미 포트 내부 상수 RSP를 복제해서 반환)
        private byte[] GetSimulatedResponse(DummySerialPort dummy, ProtocolKind kind)
        //private byte[] GetSimulatedResponse()
        {
            try
            {
                switch (kind)
                {
                    case DummySerialPortNs.ProtocolKind.ErrorDataRequest:
                        return DummySerialPortNs.DummySerialPort.RSP_ErrorData;
                    case DummySerialPortNs.ProtocolKind.ErrorReset:

                    case DummySerialPortNs.ProtocolKind.StartStopStatus:
                        return DummySerialPortNs.DummySerialPort.RSP_StartStopStatus;
                    case DummySerialPortNs.ProtocolKind.ParameterRequest:
                        return DummySerialPortNs.DummySerialPort.RSP_ParameterRequest;
                    case DummySerialPortNs.ProtocolKind.ParameterSet:

                    default:
                        return DummySerialPortNs.DummySerialPort.RSP_StartStopStatus;
                        /*
                        case ProtocolKind.ErrorDataRequest:
                            return (byte[])dummy.RSP_ErrorData.Clone();
                        case ProtocolKind.ParameterRequest:
                            return (byte[])dummy.RSP_ParameterRequest.Clone();
                        case ProtocolKind.ParameterSet:

                        case ProtocolKind.StartStopStatus:
                            return (byte[])dummy.RSP_StartStopStatus.Clone();
                        default:
                            return (byte[])dummy.RSP_StartStopStatus.Clone();
                        */
                }
            }
            catch
            {
                return Array.Empty<byte>();
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

        private void EnsureTimer()
        {
            if (timer == null)
            {
                timer = new System.Timers.Timer();
                timer.Interval = 1000;
                timer.AutoReset = true;
            }
            // 중복 방지: 이벤트 핸들러를 항상 ‘먼저 제거 → 다시 등록’
            timer.Elapsed -= Timer_Elapsed;
            timer.Elapsed += Timer_Elapsed;
        }

        private void StartTimerSafe()
        {
            EnsureTimer();
            timer.Stop();   // 상태 초기화
            timer.Start();  // 확실히 시작
        }

        private void StopTimerSafe()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= Timer_Elapsed; // 재연결 시 중복 방지
            }
        }

        // UI 스레드에서만 호출되어 캐시 업데이트
        private void UseDummyCheck_Checked(object sender, RoutedEventArgs e)
        {
            _useDummyCached = true;
        }
        private void UseDummyCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            _useDummyCached = false;
        }

        private List<byte[]> ExtractFramesFromBuffer(List<byte> buf, bool isNewVersion)
        {
            // NewVersion: STX=0x12, VER=0x01, ETX=0x34
            // OldVersion: STX=0xCC, VER=0x00, ETX=0xEF
            byte stx = isNewVersion ? (byte)0x12 : (byte)0xCC;
            byte ver = isNewVersion ? (byte)0x01 : (byte)0x00;
            byte etx = isNewVersion ? (byte)0x34 : (byte)0xEF;

            var frames = new List<byte[]>();

            while (true)
            {
                // 1) STX 찾기
                int stxPos = buf.IndexOf(stx);
                if (stxPos < 0)
                {
                    // STX가 없다면 전부 노이즈로 보고 비움
                    buf.Clear();
                    break;
                }

                // STX 앞 찌꺼기 제거
                if (stxPos > 0)
                    buf.RemoveRange(0, stxPos);

                // 2) 최소 헤더(0..3) 확보: STX VER CMD SIZE
                if (buf.Count < 4)
                    break;

                // VER 확인
                if (buf[1] != ver)
                {
                    // STX는 맞았지만 다음 바이트가 기대 VER이 아님 → STX 1바이트 버리고 재탐색
                    buf.RemoveAt(0);
                    continue;
                }

                // CMD 확인(원하는 커맨드만 프레임으로 인정)
                byte cmd = buf[2];
                if (!_allowedCmd.Contains(cmd))
                {
                    buf.RemoveAt(0);
                    continue;
                }

                // 3) SIZE 읽기
                int size = buf[3];

                // SIZE sanity 체크
                if (size < MIN_FRAME_LEN || size > MAX_FRAME_LEN)
                {
                    buf.RemoveAt(0);
                    continue;
                }

                // 아직 프레임 전체가 안 모였으면 대기
                if (buf.Count < size)
                    break;

                // 4) ETX 확인 (프레임 끝)
                if (buf[size - 1] != etx)
                {
                    // 경계가 밀렸거나 가짜 STX
                    buf.RemoveAt(0);
                    continue;
                }

                // 5) 프레임 추출 + 버퍼에서 소비
                byte[] frame = buf.GetRange(0, size).ToArray();
                frames.Add(frame);
                buf.RemoveRange(0, size);
            }

            return frames;
        }

    }
}
