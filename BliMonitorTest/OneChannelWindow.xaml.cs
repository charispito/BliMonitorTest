using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.server;
using BliMonitorTest.util;
using log4net;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using FontWeights = System.Windows.FontWeights;
using Timer = System.Timers.Timer;
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

        private volatile bool _useDummyCached;
        private bool UseDummy => _useDummyCached;

        private byte[] _dummyLastBuffer;
        private readonly DummyValueGenerator _dummyGen = new DummyValueGenerator();

        private readonly object _connWatchLock = new object();
        private System.Timers.Timer _connectionWatchdog;
        private DateTime _lastResponseAt = DateTime.MinValue;
        private bool _waitingFirstResponse = false;
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(5);

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

            _useDummyCached = (UseDummyCheck?.IsChecked == true);
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

        public bool IsDummyMode
        {
            get { return _useDummyCached; }
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
                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(channel.Item3Check, new object[0]);

                typeof(System.Windows.Controls.Primitives.ButtonBase)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(channel.Item3Check, new object[0]);
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
            if (channel.ConnectState != 1) return;

            try
            {
                if (!UseDummy)
                {
                    if (port != null && port.IsOpen)
                    {
                        TestTime += TimeSpan.FromSeconds(1);

                        if (parameterCnt > 0)
                        {
                            receivedData.Clear();
                            parameterCnt--;
                        }
                        else
                        {
                            byte[] command = Protocol.GetStatusRequest();
                            port.Write(command, 0, command.Length);
                            command.PrintHex(1);
                        }
                    }
                }
                else
                {
                    TestTime += TimeSpan.FromSeconds(1);

                    var rsp = new byte[37];
                    var sample = _dummyGen.Next();

                    DummyValueGenerator.PatchStatusResponse37(rsp, sample);

                    _dummyLastBuffer = rsp;

                    log.Debug("============ LOG DATA OneChannelWindow [DoPeriodicTickCore] MAKE STATUS PACKET START ============");
                    ByteLogHelper.LogPacket(rsp, "RX");
                    ByteLogHelper.ToHexWith0x(rsp);
                    ByteLogHelper.DumpLinesWith0x(rsp, 16);
                    log.Debug("============ LOG DATA OneChannelWindow [DoPeriodicTickCore] MAKE STATUS PACKET END ============");

                    InvokePortDataReceivedWith(rsp);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (UseDummy) return;

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
                    _lastResponseAt = DateTime.Now;
                    _waitingFirstResponse = false;
                    StopConnectionWatchdog();

                    ByteLogHelper.LogPacket(buf, "RX");
                    receiveData(buf, buf.Length);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (channel.ConnectState == 1)
                {
                    StopTimerSafe();
                    StopConnectionWatchdog();

                    try { port.DataReceived -= Port_DataReceived; } catch { }

                    if (channel.streamWriter != null)
                    {
                        try { channel.streamWriter.Close(); } catch { }
                        channel.streamWriter = null;
                    }

                    if (port != null && port.IsOpen)
                    {
                        try { port.Close(); } catch { }
                    }

                    lock (_rxLock)
                    {
                        _rxBuffer.Clear();
                    }

                    // 연결이 끊어졌을 때 ParameterWindow가 열려있으면 강제로 닫기
                    CloseParameterWindowIfOpen();

                    channel.ConnectState = 0;
                    ConnectButton.Content = "연결";
                    return;
                }

                if (!UseDummy)
                {
                    if (PortList.box.SelectedIndex == -1)
                    {
                        ToastMessage.ToastService.AppToast.Show("포트가 선택 되지 않았습니다.");
                        return;
                    }

                    port.PortName = PortList.box.SelectedItem.ToString();

                    try { port.DataReceived -= Port_DataReceived; } catch { }
                    if (!port.IsOpen)
                        port.Open();
                    port.DataReceived += Port_DataReceived;
                }
                else
                {
                    lock (_rxLock)
                    {
                        _rxBuffer.Clear();
                    }
                }

                channel.ConnectState = 1;
                ConnectButton.Content = "해제";

                StartTimerSafe();
                StartConnectionWatchdog();
                DoPeriodicTickCore();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                ToastMessage.ToastService.AppToast.Show("연결 처리 중 문제가 발생했습니다.");
            }
        }

        private void InvokePortDataReceivedWith(byte[] buf)
        {
            if (buf == null || buf.Length == 0) return;

            Dispatcher.Invoke(() =>
            {
                _lastResponseAt = DateTime.Now;
                _waitingFirstResponse = false;
                StopConnectionWatchdog();

                receiveData(buf, buf.Length);
            });
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
            item.Item1.label.Content = "온수 Temp";
            item.Item2.label.Content = "냉수 Temp";
            item.Item3.label.Content = "초기급수 완료";
            item.Item4.label.Content = "초기급수 진행";
            item.Item5.label.Content = "물부족 감지";
            item.Item6.label.Content = "버퍼수위 부족";
            item.Item7.label.Content = "재가열 동작";
            item.Item8.label.Content = "가열 진행";
            item.Item9.label.Content = "히터 PWM";
            item.Item10.label.Content = "야간 상태";
            item.Item17.label.Content = "Float Stable";
            item.Item18.label.Content = "BallTop Stable";
            item.Item19.label.Content = "WaterBuf Stable";

            item.Item11.label.Content = "테스트 모드";
            item.Item12.label.Content = "제품 모델";
            item.Item13.label.Content = "에러 코드";
            item.Item14.label.Content = "모드 / 용량";
            item.Item15.label.Content = "출수 단계";
            item.Item16.label.Content = "출수 세부단계";
            item.Item20.label.Content = "히터 출력";
            item.Item21.label.Content = "컴프 출력";
            item.Item22.label.Content = "온수 밸브";
            item.Item23.label.Content = "냉수 선택 밸브";
            item.Item24.label.Content = "출수 밸브";
            item.Item25.label.Content = "버튼 상태";
            item.Item26.label.Content = "상태 비트";

            item.Item40.label.Content = "A Heater";
            item.Item41.label.Content = "A Comp";
            item.Item42.label.Content = "A HotValve";
            item.Item43.label.Content = "A ColdSel";
            item.Item44.label.Content = "A Outlet";
            item.Item45.label.Content = "A PumpOut";
            item.Item46.label.Content = "A PumpDia";
            item.Item47.label.Content = "A Airvent";
            item.Item48.label.Content = "B Float";
            item.Item49.label.Content = "B BallTop";
            item.Item50.label.Content = "B WaterBuf";
            item.Item51.label.Content = "B Empty";
            item.Item52.label.Content = "B BufLow";
            item.Item53.label.Content = "B Reheat";
            item.Item54.label.Content = "B HotIng";
            item.Item55.label.Content = "B Disp";

            item.Statebox.label.Content = "상태 / 에러";
            item.ChannelView.label.Content = "연결상태";
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
                    if (channel.Item1Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[10] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, "온수 Temp Raw");
                        }
                        else
                        {
                            channel.Item1Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list1.Clear();
                        if (seriesList.ContainsKey(10))
                        {
                            int index = seriesList[10];
                            seriesList.Remove(10);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item2Check":
                    if (channel.Item2Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[11] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, "냉수 Temp Raw");
                        }
                        else
                        {
                            channel.Item2Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list2.Clear();
                        if (seriesList.ContainsKey(11))
                        {
                            int index = seriesList[11];
                            seriesList.Remove(11);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item3Check":
                    if (channel.Item3Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[12] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "초기급수 완료");
                        }
                        else
                        {
                            channel.Item3Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list3.Clear();
                        if (seriesList.ContainsKey(12))
                        {
                            int index = seriesList[12];
                            seriesList.Remove(12);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item4Check":
                    if (channel.Item4Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[13] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "물부족 감지");
                        }
                        else
                        {
                            channel.Item4Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list4.Clear();
                        if (seriesList.ContainsKey(13))
                        {
                            int index = seriesList[13];
                            seriesList.Remove(13);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item5Check":
                    if (channel.Item5Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[14] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "히터 출력");
                        }
                        else
                        {
                            channel.Item5Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list5.Clear();
                        if (seriesList.ContainsKey(14))
                        {
                            int index = seriesList[14];
                            seriesList.Remove(14);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item6Check":
                    if (channel.Item6Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[15] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "컴프 출력");
                        }
                        else
                        {
                            channel.Item6Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list6.Clear();
                        if (seriesList.ContainsKey(15))
                        {
                            int index = seriesList[15];
                            seriesList.Remove(15);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item7Check":
                    if (channel.Item7Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[16] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "온수 밸브");
                        }
                        else
                        {
                            channel.Item7Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list7.Clear();
                        if (seriesList.ContainsKey(16))
                        {
                            int index = seriesList[16];
                            seriesList.Remove(16);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;

                case "Item8Check":
                    if (channel.Item8Check.IsChecked == true)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[17] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, "출수 밸브");
                        }
                        else
                        {
                            channel.Item8Check.IsChecked = false;
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        channel.list8.Clear();
                        if (seriesList.ContainsKey(17))
                        {
                            int index = seriesList[17];
                            seriesList.Remove(17);
                            Chart.ViewModel.unSetSeries(index);
                            Chart.setLegend(index, "");
                        }
                    }
                    break;
            }
        }

        private void StartConnectionWatchdog()
        {
            lock (_connWatchLock)
            {
                StopConnectionWatchdog();

                _waitingFirstResponse = true;
                _lastResponseAt = DateTime.Now;

                _connectionWatchdog = new System.Timers.Timer(500);
                _connectionWatchdog.AutoReset = true;
                _connectionWatchdog.Elapsed += ConnectionWatchdog_Elapsed;
                _connectionWatchdog.Start();
            }
        }

        private void StopConnectionWatchdog()
        {
            lock (_connWatchLock)
            {
                if (_connectionWatchdog != null)
                {
                    try
                    {
                        _connectionWatchdog.Stop();
                        _connectionWatchdog.Elapsed -= ConnectionWatchdog_Elapsed;
                        _connectionWatchdog.Dispose();
                    }
                    catch { }
                    _connectionWatchdog = null;
                }

                _waitingFirstResponse = false;
            }
        }

        private void ConnectionWatchdog_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!_waitingFirstResponse) return;

                if (DateTime.Now - _lastResponseAt < _connectionTimeout)
                    return;

                _waitingFirstResponse = false;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        StopTimerSafe();
                        StopConnectionWatchdog();

                        try { port.DataReceived -= Port_DataReceived; } catch { }
                        try
                        {
                            if (port != null && port.IsOpen)
                                port.Close();
                        }
                        catch { }

                        lock (_rxLock)
                        {
                            _rxBuffer.Clear();
                        }

                        // 연결이 끊어졌을 때 ParameterWindow가 열려있으면 강제로 닫기
                        CloseParameterWindowIfOpen();

                        channel.ConnectState = 0;
                        ConnectButton.Content = "연결";

                        ToastMessage.ToastService.AppToast.Show("장비 응답이 없어 연결을 자동 해제했습니다.");
                    }
                    catch (Exception ex)
                    {
                        log.Warn("ConnectionWatchdog 자동 해제 실패", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                log.Warn("ConnectionWatchdog_Elapsed 실패", ex);
            }
        }

        private void CloseParameterWindowIfOpen()
        {
            try
            {
                if (channel?.parameterWindow != null)
                {
                    var win = channel.parameterWindow;

                    if (win.IsVisible)
                    {
                        win.Close();
                    }

                    channel.parameterWindow = null;
                }
            }
            catch (Exception ex)
            {
                log.Warn("ParameterWindow 강제 종료 실패", ex);
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

            timer.Elapsed -= Timer_Elapsed;
            timer.Elapsed += Timer_Elapsed;
        }

        private void StartTimerSafe()
        {
            EnsureTimer();
            timer.Stop();
            timer.Start();
        }

        private void StopTimerSafe()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= Timer_Elapsed;
            }
        }

        private void UseDummyCheck_Checked(object sender, RoutedEventArgs e)
        {
            _useDummyCached = true;
        }

        private void UseDummyCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            _useDummyCached = false;
        }
    }
}
