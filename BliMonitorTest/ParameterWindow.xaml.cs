using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Xml.Linq;
using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.setting;
using BliMonitorTest.util;
using BliMonitorTest.util.MonitoringDb;
using log4net;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;

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

        // ===== 자동 저장 디바운스 =====
        private System.Threading.CancellationTokenSource _autoSaveCts;
        private TimeSpan _autoSaveDebounce = TimeSpan.FromMilliseconds(400);

        private ICollectionView _fileView;
        private ICollectionView _errorView;

        // 휴지통 실패 시 하드 삭제 폴백 여부(필요하면 true로 켬)
        private bool _allowHardDeleteFallback = false;

        // 에러 데이터 DB저장 관련 상수
        private readonly int _channelNoForDb = 1;

        private Duo8ErrorResponse _lastErrorResponse;

        private Duo8StatusPacket _lastStatusPacket;
        private DateTime _lastStatusUiUpdateAt = DateTime.MinValue;

        // 상태 UI 갱신 주기 (기본 10초)
        private TimeSpan _statusUiRefreshInterval = TimeSpan.FromSeconds(10);

        public ParameterWindow(SerialPort port, OneChannelValueDetail detail)
        {
            InitializeComponent();
            this.port = port;
            oneChannel = detail;
            Initialize2();
            InitializeSetting();
            management = new ConfigFileManagement();
            SetList();
            SetErrorList();
        }

        public ParameterWindow()
        {
            InitializeComponent();
            InitializeSetting();
            management = new ConfigFileManagement();
            SetList();
            SetErrorList();
            Initialize2();
            //Initialize();
        }


        private void InitializeSetting()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParameterSetting");
            //string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void Initialize2()
        {
            /*
            ObservableCollection<ErrorData> list = new ObservableCollection<ErrorData>();
            list.Add(new ErrorData() { Name = "유효" });
            list.Add(new ErrorData() { Name = "SEQ" });
            list.Add(new ErrorData() { Name = "에러코드" });
            list.Add(new ErrorData() { Name = "에러명" });
            list.Add(new ErrorData() { Name = "온수 Temp" });
            list.Add(new ErrorData() { Name = "냉수 Temp" });
            list.Add(new ErrorData() { Name = "ADC HOT" });
            list.Add(new ErrorData() { Name = "ADC COLD" });
            
            ErrorGrid.ItemsSource = list;
            */

            ErrorGrid.ItemsSource = BuildDefaultErrorRows();

            InitBottomDuo8Editor();
            CopyToEditButton.Click += CopyToEditButton_Click;

            SaveButton.Click += SaveButton_Click;
            DeleteButton.Click += DeleteButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            ErrorSaveButton.Click += ErrorSaveButton_Click;
            ErrorDeleteButton.Click += ErrorDeleteButton_Click;
            ErrorRefreshButton.Click += ErrorRefreshButton_Click;
            ErrorExcelButton.Click += ErrorExcelButton_Click;
            OpenErrorDataQueryButton.Click += OpenErrorDataQueryButton_Click;

            // 검색 필터 이벤트 연결 (XAML의 x:Name 동일 가정)
            FileSearchBox.TextChanged += FileSearchBox_TextChanged;
            ErrorSearchBox.TextChanged += ErrorSearchBox_TextChanged;

            OneChannelWindow parent = Window.GetWindow(oneChannel) as OneChannelWindow;
            bool isDummyMode = parent != null && parent.IsDummyMode;

            // 더미가 체크되었다면 더미데이터 설정
            if (port != null || isDummyMode)
            {
                Loaded += ParameterWindow_Loaded1;
                Closed += ParameterWindow_Closed1;
                ReadParamButton.Click += ReadParamButton_Click;
                ReadErrorButton.Click += (s, e) =>
                {
                    if (_isReadingError)
                    {
                        log.Info("ReadErrorButton: 이전 요청 처리 중. 클릭 무시");
                        return;
                    }

                    try
                    {
                        _isReadingError = true;
                        ReadErrorButton.IsEnabled = false;

                        if (isDummyMode)
                        {
                            var gen = new BliMonitorTest.dummy.DummyValueGenerator();
                            byte[] dummyErr = gen.BuildDummyErrorResponse();
                            setError(dummyErr);
                            ToastMessage.ToastService.AppToast.Show("더미 에러 정보를 갱신했습니다.");
                            return;
                        }

                        byte[] command = Protocol.GetError();
                        command.PrintHex(1);

                        if (port != null && port.IsOpen)
                        {
                            oneChannel.setParameter();
                            port.Write(command, 0, command.Length);
                        }
                        else
                        {
                            ToastMessage.ToastService.AppToast.Show("포트가 연결되어 있지 않습니다.");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("ReadErrorButton 전송 실패", ex);
                        ToastMessage.ToastService.AppToast.Show("에러 요청 전송 중 문제가 발생했습니다.");
                    }
                    finally
                    {
                        _isReadingError = false;
                        ReadErrorButton.IsEnabled = true;
                    }
                };

                WriteParamButton.Click += WriteParamButton_Click;
                ResetErrorButton.Click += ResetErrorButton_Click;
            }

            
            // 신규 제품에서는 파라미터 제어 기능 제외
            if (ReadParamButton != null) ReadParamButton.IsEnabled = true;
            if (WriteParamButton != null) WriteParamButton.IsEnabled = false;
            if (CopyToEditButton != null) CopyToEditButton.IsEnabled = true;

            // 에러그리드 초기값 셋팅
            SetDefaultErrorGrid();

            // 하단을 상태정보 표시용으로 사용
            SetBottomStatusGridDefaults();

            InitCollectionViews();
            this.KeyDown += Window_KeyDown;
        }

        private ObservableCollection<StatusViewRow> _receivedRows = new ObservableCollection<StatusViewRow>();
        private ObservableCollection<StatusViewRow> _editableRows = new ObservableCollection<StatusViewRow>();

        private void InitBottomDuo8Editor()
        {
            try
            {
                _receivedRows = BuildEmptyDuo8RowSet();
                _editableRows = BuildEmptyDuo8RowSet();

                ReceivedParamGrid.ItemsSource = ToTwoColumnRows(_receivedRows);
                EditableParamGrid.ItemsSource = ToTwoColumnRows(_editableRows);
            }
            catch (Exception ex)
            {
                log.Warn("InitBottomDuo8Editor 실패", ex);
            }
        }

        private ObservableCollection<StatusViewRow> BuildEmptyDuo8RowSet()
        {
            return new ObservableCollection<StatusViewRow>
            {
                new StatusViewRow { Name = "제품코드", Value = "" },
                new StatusViewRow { Name = "에러코드", Value = "" },
                new StatusViewRow { Name = "초기급수 완료", Value = "" },
                new StatusViewRow { Name = "초기급수 진행", Value = "" },
                new StatusViewRow { Name = "물부족 감지", Value = "" },
                new StatusViewRow { Name = "버퍼수위 부족", Value = "" },
                new StatusViewRow { Name = "재가열 동작", Value = "" },
                new StatusViewRow { Name = "가열 진행", Value = "" },
                new StatusViewRow { Name = "히터 PWM", Value = "" },
                new StatusViewRow { Name = "야간 상태", Value = "" },
                new StatusViewRow { Name = "테스트 모드", Value = "" },
                new StatusViewRow { Name = "선택 모드", Value = "" },
                new StatusViewRow { Name = "선택 용량", Value = "" },
                new StatusViewRow { Name = "출수 단계", Value = "" },
                new StatusViewRow { Name = "출수 세부단계", Value = "" },
                new StatusViewRow { Name = "온수 Temp Raw", Value = "" },
                new StatusViewRow { Name = "냉수 Temp Raw", Value = "" },
                new StatusViewRow { Name = "Float Stable", Value = "" },
                new StatusViewRow { Name = "BallTop Stable", Value = "" },
                new StatusViewRow { Name = "WaterBuf Stable", Value = "" },
                new StatusViewRow { Name = "히터 출력", Value = "" },
                new StatusViewRow { Name = "컴프 출력", Value = "" },
                new StatusViewRow { Name = "온수 밸브", Value = "" },
                new StatusViewRow { Name = "냉수 선택 밸브", Value = "" },
                new StatusViewRow { Name = "출수 밸브", Value = "" },

                new StatusViewRow { Name = "Button HOT 선택", Value = "" },
                new StatusViewRow { Name = "Button WARM 선택", Value = "" },
                new StatusViewRow { Name = "Button NORMAL 선택", Value = "" },
                new StatusViewRow { Name = "Button COOL 선택", Value = "" },
                new StatusViewRow { Name = "Button COLD 선택", Value = "" },
                new StatusViewRow { Name = "Button REHEAT", Value = "" },
                new StatusViewRow { Name = "Button 150mL", Value = "" },
                new StatusViewRow { Name = "Button 1000mL", Value = "" },
                new StatusViewRow { Name = "Button OUTLET", Value = "" },

                new StatusViewRow { Name = "A Heater", Value = "" },
                new StatusViewRow { Name = "A Comp", Value = "" },
                new StatusViewRow { Name = "A HotValve", Value = "" },
                new StatusViewRow { Name = "A ColdSel", Value = "" },
                new StatusViewRow { Name = "A Outlet", Value = "" },
                new StatusViewRow { Name = "A PumpOut", Value = "" },
                new StatusViewRow { Name = "A PumpDia", Value = "" },
                new StatusViewRow { Name = "A Airvent", Value = "" },

                new StatusViewRow { Name = "B Float", Value = "" },
                new StatusViewRow { Name = "B BallTop", Value = "" },
                new StatusViewRow { Name = "B WaterBuf", Value = "" },
                new StatusViewRow { Name = "B Empty", Value = "" },
                new StatusViewRow { Name = "B BufLow", Value = "" },
                new StatusViewRow { Name = "B Reheat", Value = "" },
                new StatusViewRow { Name = "B HotIng", Value = "" },
                new StatusViewRow { Name = "B Disp", Value = "" },

                new StatusViewRow { Name = "ButtonInfo", Value = "" },
                new StatusViewRow { Name = "Status A", Value = "" },
                new StatusViewRow { Name = "Status B", Value = "" }
            };
        }

        private void CopyToEditButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_receivedRows == null || _receivedRows.Count == 0)
                {
                    ToastMessage.ToastService.AppToast.Show("복사할 수신 데이터가 없습니다.");
                    return;
                }

                _editableRows.Clear();

                foreach (var row in _receivedRows)
                {
                    _editableRows.Add(new StatusViewRow
                    {
                        Name = row.Name,
                        Value = row.Value
                    });
                }

                EditableParamGrid.ItemsSource = null;
                EditableParamGrid.ItemsSource = ToTwoColumnRows(_editableRows);

                RightSet = true;
                ToastMessage.ToastService.AppToast.Show("좌측 수신 데이터를 우측 편집 영역으로 복사했습니다.");
            }
            catch (Exception ex)
            {
                log.Warn("CopyToEditButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("복사 중 문제가 발생했습니다.");
            }
        }

        private void SetBottomStatusGridDefaults()
        {
            try
            {
                _receivedRows = BuildEmptyDuo8RowSet();
                _editableRows = BuildEmptyDuo8RowSet();

                if (ReceivedParamGrid != null)
                    ReceivedParamGrid.ItemsSource = ToTwoColumnRows(_receivedRows);

                if (EditableParamGrid != null)
                    EditableParamGrid.ItemsSource = ToTwoColumnRows(_editableRows);
            }
            catch (Exception ex)
            {
                log.Warn("SetBottomStatusGridDefaults 실패", ex);
            }
        }

        public void setStatus(byte[] data)
        {
            Duo8StatusPacket pkt = Duo8PacketParser.ParseStatus(data);
            if (pkt == null)
                return;

            _lastStatusPacket = pkt;

            DateTime now = DateTime.Now;
            if ((now - _lastStatusUiUpdateAt) < _statusUiRefreshInterval)
                return;

            _lastStatusUiUpdateAt = now;
            RefreshStatusUi(pkt);
        }

        public void SetStatusUiRefreshIntervalSeconds(int seconds)
        {
            if (seconds < 1)
                seconds = 1;

            _statusUiRefreshInterval = TimeSpan.FromSeconds(seconds);
        }

        private void RefreshStatusUi(Duo8StatusPacket pkt)
        {
            if (pkt == null)
                return;

            ushort buttonInfo = pkt.ButtonInfo;
            byte statusA = pkt.StatusA;
            byte statusB = pkt.StatusB;

            var statusList = new ObservableCollection<BliMonitorTest.data.StatusViewRow>();

            statusList.Add(new StatusViewRow() { Name = "제품코드", Value = Duo8ValueText.GetModelName(pkt.ModelCode) + $" (0x{pkt.ModelCode:X2})" });
            statusList.Add(new StatusViewRow() { Name = "에러코드", Value = $"0x{pkt.ErrorCode:X2} / {Duo8ValueText.GetErrorText(pkt.ErrorCode)}" });
            statusList.Add(new StatusViewRow() { Name = "초기급수 완료", Value = Duo8ValueText.ToYesNo(pkt.WaterInitDone) });
            statusList.Add(new StatusViewRow() { Name = "초기급수 진행", Value = Duo8ValueText.ToYesNo(pkt.WaterInitGo) });
            statusList.Add(new StatusViewRow() { Name = "물부족 감지", Value = Duo8ValueText.ToYesNo(pkt.EmptyDetect) });
            statusList.Add(new StatusViewRow() { Name = "버퍼수위 부족", Value = Duo8ValueText.ToYesNo(pkt.BufferLow) });
            statusList.Add(new StatusViewRow() { Name = "재가열 동작", Value = Duo8ValueText.ToOnOff(pkt.ReheatRunning) });
            statusList.Add(new StatusViewRow() { Name = "가열 진행", Value = Duo8ValueText.ToOnOff(pkt.HotIng) });
            statusList.Add(new StatusViewRow() { Name = "히터 PWM", Value = pkt.HeaterPwm.ToString() });
            statusList.Add(new StatusViewRow() { Name = "야간 상태", Value = Duo8ValueText.ToOnOff(pkt.Night) });
            statusList.Add(new StatusViewRow() { Name = "테스트 모드", Value = Duo8ValueText.ToOnOff(pkt.TestMode) });
            statusList.Add(new StatusViewRow() { Name = "선택 모드", Value = Duo8ValueText.GetModeText(pkt.ModeSelected) });
            statusList.Add(new StatusViewRow() { Name = "선택 용량", Value = Duo8ValueText.GetQtyText(pkt.QtySelected) });
            statusList.Add(new StatusViewRow() { Name = "출수 단계", Value = Duo8ValueText.GetDispensePhaseText(pkt.DispensePhase) });
            statusList.Add(new StatusViewRow() { Name = "출수 세부단계", Value = Duo8ValueText.GetDispenseSubPhaseText(pkt.DispenseSubPhase) });
            statusList.Add(new StatusViewRow() { Name = "온수 Temp Raw", Value = pkt.HotTempRaw.ToString() });
            statusList.Add(new StatusViewRow() { Name = "냉수 Temp Raw", Value = pkt.ColdTempRaw.ToString() });
            statusList.Add(new StatusViewRow() { Name = "Float Stable", Value = Duo8ValueText.ToYesNo(pkt.FloatLowStable) });
            statusList.Add(new StatusViewRow() { Name = "BallTop Stable", Value = Duo8ValueText.ToYesNo(pkt.BallTopFullStable) });
            statusList.Add(new StatusViewRow() { Name = "WaterBuf Stable", Value = Duo8ValueText.ToYesNo(pkt.WaterBufFullStable) });
            statusList.Add(new StatusViewRow() { Name = "히터 출력", Value = Duo8ValueText.ToOnOff(pkt.HeaterOutput) });
            statusList.Add(new StatusViewRow() { Name = "컴프 출력", Value = Duo8ValueText.ToOnOff(pkt.CompressorOutput) });
            statusList.Add(new StatusViewRow() { Name = "온수 밸브", Value = Duo8ValueText.ToOnOff(pkt.HotValveOutput) });
            statusList.Add(new StatusViewRow() { Name = "냉수 선택 밸브", Value = Duo8ValueText.ToOnOff(pkt.ColdSelectOutput) });
            statusList.Add(new StatusViewRow() { Name = "출수 밸브", Value = Duo8ValueText.ToOnOff(pkt.OutletValveOutput) });

            statusList.Add(new StatusViewRow() { Name = "HOT 선택", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0001) != 0) });
            statusList.Add(new StatusViewRow() { Name = "WARM 선택", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0002) != 0) });
            statusList.Add(new StatusViewRow() { Name = "NORMAL 선택", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0004) != 0) });
            statusList.Add(new StatusViewRow() { Name = "COOL 선택", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0008) != 0) });
            statusList.Add(new StatusViewRow() { Name = "COLD 선택", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0010) != 0) });
            statusList.Add(new StatusViewRow() { Name = "REHEAT", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0020) != 0) });
            statusList.Add(new StatusViewRow() { Name = "150mL", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0040) != 0) });
            statusList.Add(new StatusViewRow() { Name = "1000mL", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0080) != 0) });
            statusList.Add(new StatusViewRow() { Name = "OUTLET", Value = Duo8ValueText.GetBitState((buttonInfo & 0x0100) != 0) });

            statusList.Add(new StatusViewRow() { Name = "A Heater", Value = Duo8ValueText.GetBitState((statusA & 0x01) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Compressor", Value = Duo8ValueText.GetBitState((statusA & 0x02) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Hot Valve", Value = Duo8ValueText.GetBitState((statusA & 0x04) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Cold Select", Value = Duo8ValueText.GetBitState((statusA & 0x08) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Outlet Valve", Value = Duo8ValueText.GetBitState((statusA & 0x10) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Pump Outlet", Value = Duo8ValueText.GetBitState((statusA & 0x20) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Pump Diaphragm", Value = Duo8ValueText.GetBitState((statusA & 0x40) != 0) });
            statusList.Add(new StatusViewRow() { Name = "A Pump Airvent", Value = Duo8ValueText.GetBitState((statusA & 0x80) != 0) });

            statusList.Add(new StatusViewRow() { Name = "B Float Sensor", Value = Duo8ValueText.GetBitState((statusB & 0x01) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Ball Top", Value = Duo8ValueText.GetBitState((statusB & 0x02) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Water Buffer", Value = Duo8ValueText.GetBitState((statusB & 0x04) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Empty Detect", Value = Duo8ValueText.GetBitState((statusB & 0x08) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Buffer Low", Value = Duo8ValueText.GetBitState((statusB & 0x10) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Reheat Running", Value = Duo8ValueText.GetBitState((statusB & 0x20) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Hot Ing", Value = Duo8ValueText.GetBitState((statusB & 0x40) != 0) });
            statusList.Add(new StatusViewRow() { Name = "B Dispensing", Value = Duo8ValueText.GetBitState((statusB & 0x80) != 0) });

            statusList.Add(new StatusViewRow() { Name = "Button Raw", Value = $"0x{pkt.ButtonInfo:X4}" });
            statusList.Add(new StatusViewRow() { Name = "StatusA Raw", Value = $"0x{pkt.StatusA:X2}" });
            statusList.Add(new StatusViewRow() { Name = "StatusB Raw", Value = $"0x{pkt.StatusB:X2}" });

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _receivedRows = statusList;
                ReceivedParamGrid.ItemsSource = ToTwoColumnRows(_receivedRows);
            }));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+A → 현재 포커스 리스트 전체 선택
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
            // Delete → 휴지통으로 삭제, Shift+Delete → 영구 삭제
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
            // Extended 모드에서만 동작
            if (lb.SelectionMode != SelectionMode.Extended) return;

            // 모든 아이템 선택
            lb.SelectedItems.Clear();
            foreach (var item in lb.Items)
                lb.SelectedItems.Add(item);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetList();
            SetErrorList();
            ClearParameterFileInputs();
            ClearErrorFileInputs();
            ToastMessage.ToastService.AppToast.Show("파일 목록을 새로고침했습니다.");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedCore(FileList.SelectedItems, hardDelete: false, kind: "Param");
            ClearParameterFileInputs();
        }

        private void ListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ListBoxItem item = sender as ListBoxItem;
                if (item == null || item.Content == null)
                    return;

                string fileName = item.Content.ToString();
                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                FileName.Text = fileName;
                LoadParameterFile(fileName);
                ToastMessage.ToastService.AppToast.Show("파라미터 파일을 불러왔습니다.");
            }
            catch (Exception ex)
            {
                log.Error("ListDoubleClick 실패", ex);
                ToastMessage.ToastService.AppToast.Show("파라미터 파일을 불러오는 중 문제가 발생했습니다.");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FileName.Text))
                {
                    ToastMessage.ToastService.AppToast.Show("파일명을 입력 하세요.");
                    return;
                }

                if (_editableRows == null || _editableRows.Count == 0)
                {
                    ToastMessage.ToastService.AppToast.Show("저장할 파라미터 정보가 없습니다.");
                    return;
                }

                SaveParameterFile(FileName.Text.Trim());
                SetList();
                ClearParameterFileInputs();

                ToastMessage.ToastService.AppToast.Show("파라미터 정보를 저장했습니다.");
            }
            catch (Exception ex)
            {
                log.Error("SaveButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("파라미터 저장 중 문제가 발생했습니다.");
            }
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
                    {
                        files.Add(System.IO.Path.GetFileNameWithoutExtension(fi.Name));
                    }
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
                    {
                        errorFiles.Add(System.IO.Path.GetFileNameWithoutExtension(file.Name));
                    }
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
            try
            {
                if (_editableRows == null || _editableRows.Count == 0)
                {
                    ToastMessage.ToastService.AppToast.Show("쓰기 대상 파라미터가 없습니다.");
                    return;
                }

                if (!RightSet)
                {
                    ToastMessage.ToastService.AppToast.Show("먼저 좌측 데이터를 우측으로 복사(COPY)하세요.");
                    return;
                }

                // TODO:
                // 1. _editableRows 값을 Duo8 파라미터 Write 패킷 구조로 변환
                // 2. MCU Write 명령 프레임 생성
                // 3. port 또는 Dummy 인터페이스로 전송
                // 4. 응답 ACK 처리

                ToastMessage.ToastService.AppToast.Show("신규 제품 PARAMETER WRITE는 추후 구현 예정입니다.");
            }
            catch (Exception ex)
            {
                log.Warn("WriteParamButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("파라미터 쓰기 준비 중 문제가 발생했습니다.");
            }
        }

        private void ParameterWindow_Closed1(object sender, EventArgs e)
        {
            oneChannel.ParameterMode = false;
        }

        private void ParameterWindow_Loaded1(object sender, RoutedEventArgs e)
        {
            oneChannel.ParameterMode = true;

            try
            {
                OneChannelWindow parent = Window.GetWindow(oneChannel) as OneChannelWindow;
                bool isDummyMode = parent != null && parent.IsDummyMode;

                if (isDummyMode)
                {
                    var gen = new BliMonitorTest.dummy.DummyValueGenerator();
                    var rsp = new byte[37];
                    var sample = gen.Next();
                    BliMonitorTest.dummy.DummyValueGenerator.PatchStatusResponse37(rsp, sample);
                    setStatus(rsp);
                    return;
                }

                byte[] command = Protocol.GetParameter();
                command.PrintHex(1);

                if (port != null && port.IsOpen)
                {
                    oneChannel.setParameter();
                    port.Write(command, 0, command.Length);
                }
            }
            catch (Exception ex)
            {
                log.Warn("ParameterWindow_Loaded1 상태조회 실패", ex);
            }
        }

        private void ResetErrorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OneChannelWindow parent = Window.GetWindow(oneChannel) as OneChannelWindow;
                bool isDummyMode = parent != null && parent.IsDummyMode;

                if (isDummyMode)
                {
                    SetDefaultErrorGrid();
                    ToastMessage.ToastService.AppToast.Show("더미 에러 정보를 초기화했습니다.");
                    return;
                }

                byte[] command = Protocol.GetErrorReset();
                command.PrintHex(1);

                if (port != null && port.IsOpen)
                {
                    port.Write(command, 0, command.Length);
                    ToastMessage.ToastService.AppToast.Show("에러 리셋 요청을 전송했습니다.");
                }
                else
                {
                    ToastMessage.ToastService.AppToast.Show("포트가 연결되어 있지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                log.Error("ResetErrorButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("에러 리셋 중 문제가 발생했습니다.");
            }
        }

        private void ReadParamButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OneChannelWindow parent = Window.GetWindow(oneChannel) as OneChannelWindow;
                bool isDummyMode = parent != null && parent.IsDummyMode;

                if (isDummyMode)
                {
                    var gen = new BliMonitorTest.dummy.DummyValueGenerator();
                    var rsp = new byte[37];
                    var sample = gen.Next();
                    BliMonitorTest.dummy.DummyValueGenerator.PatchStatusResponse37(rsp, sample);
                    setStatus(rsp);

                    ToastMessage.ToastService.AppToast.Show("더미 상태정보를 갱신했습니다.");
                    return;
                }

                byte[] command = Protocol.GetParameter();
                command.PrintHex(1);

                if (port != null && port.IsOpen)
                {
                    oneChannel.setParameter();
                    port.Write(command, 0, command.Length);
                    ToastMessage.ToastService.AppToast.Show("상태정보 요청을 전송했습니다.");
                }
                else
                {
                    ToastMessage.ToastService.AppToast.Show("포트가 연결되어 있지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                log.Error("ReadParamButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("상태 조회 중 문제가 발생했습니다.");
            }
        }

        private void ErrorListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ListBoxItem item = sender as ListBoxItem;
                if (item == null || item.Content == null)
                    return;

                string fileName = item.Content.ToString();
                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
                string full = System.IO.Path.Combine(dir, fileName + ".config");
                if (!File.Exists(full))
                {
                    ToastMessage.ToastService.AppToast.Show("파일이 존재하지 않습니다.");
                    return;
                }

                var doc = XDocument.Load(full);
                var root = doc.Root;
                if (root == null)
                {
                    ToastMessage.ToastService.AppToast.Show("에러 파일 형식이 올바르지 않습니다.");
                    return;
                }

                string GetSlotValue(string nodeName, string fieldName)
                {
                    return NormalizeZero(root.Element(nodeName)?.Element(fieldName)?.Value ?? "");
                }

                byte ParseHexByte(string text)
                {
                    if (string.IsNullOrWhiteSpace(text))
                        return 0;

                    string t = text.Trim();
                    if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        t = t.Substring(2);

                    if (byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out byte hexVal))
                        return hexVal;

                    if (byte.TryParse(t, out byte decVal))
                        return decVal;

                    return 0;
                }

                string GetStatusABitFromFile(int slot, byte mask)
                {
                    string raw = GetSlotValue($"error{slot}", "StatusA");
                    byte b = ParseHexByte(raw);
                    return Duo8ValueText.GetBitState((b & mask) != 0);
                }

                string GetStatusBBitFromFile(int slot, byte mask)
                {
                    string raw = GetSlotValue($"error{slot}", "StatusB");
                    byte b = ParseHexByte(raw);
                    return Duo8ValueText.GetBitState((b & mask) != 0);
                }

                ObservableCollection<ErrorData> list = new ObservableCollection<ErrorData>();

                list.Add(new ErrorData
                {
                    Name = "유효",
                    Value = GetSlotValue("error1", "Valid"),
                    Value2 = GetSlotValue("error2", "Valid"),
                    Value3 = GetSlotValue("error3", "Valid"),
                    Value4 = GetSlotValue("error4", "Valid"),
                    Value5 = GetSlotValue("error5", "Valid"),
                    Value6 = GetSlotValue("error6", "Valid"),
                    Value7 = GetSlotValue("error7", "Valid"),
                    Value8 = GetSlotValue("error8", "Valid")
                });

                list.Add(new ErrorData
                {
                    Name = "SEQ",
                    Value = GetSlotValue("error1", "Seq"),
                    Value2 = GetSlotValue("error2", "Seq"),
                    Value3 = GetSlotValue("error3", "Seq"),
                    Value4 = GetSlotValue("error4", "Seq"),
                    Value5 = GetSlotValue("error5", "Seq"),
                    Value6 = GetSlotValue("error6", "Seq"),
                    Value7 = GetSlotValue("error7", "Seq"),
                    Value8 = GetSlotValue("error8", "Seq")
                });

                list.Add(new ErrorData
                {
                    Name = "에러코드",
                    Value = GetSlotValue("error1", "ErrorCode"),
                    Value2 = GetSlotValue("error2", "ErrorCode"),
                    Value3 = GetSlotValue("error3", "ErrorCode"),
                    Value4 = GetSlotValue("error4", "ErrorCode"),
                    Value5 = GetSlotValue("error5", "ErrorCode"),
                    Value6 = GetSlotValue("error6", "ErrorCode"),
                    Value7 = GetSlotValue("error7", "ErrorCode"),
                    Value8 = GetSlotValue("error8", "ErrorCode")
                });

                list.Add(new ErrorData
                {
                    Name = "에러명",
                    Value = GetSlotValue("error1", "ErrorName"),
                    Value2 = GetSlotValue("error2", "ErrorName"),
                    Value3 = GetSlotValue("error3", "ErrorName"),
                    Value4 = GetSlotValue("error4", "ErrorName"),
                    Value5 = GetSlotValue("error5", "ErrorName"),
                    Value6 = GetSlotValue("error6", "ErrorName"),
                    Value7 = GetSlotValue("error7", "ErrorName"),
                    Value8 = GetSlotValue("error8", "ErrorName")
                });

                list.Add(new ErrorData
                {
                    Name = "온수 Temp",
                    Value = GetSlotValue("error1", "HotTemp"),
                    Value2 = GetSlotValue("error2", "HotTemp"),
                    Value3 = GetSlotValue("error3", "HotTemp"),
                    Value4 = GetSlotValue("error4", "HotTemp"),
                    Value5 = GetSlotValue("error5", "HotTemp"),
                    Value6 = GetSlotValue("error6", "HotTemp"),
                    Value7 = GetSlotValue("error7", "HotTemp"),
                    Value8 = GetSlotValue("error8", "HotTemp")
                });

                list.Add(new ErrorData
                {
                    Name = "냉수 Temp",
                    Value = GetSlotValue("error1", "ColdTemp"),
                    Value2 = GetSlotValue("error2", "ColdTemp"),
                    Value3 = GetSlotValue("error3", "ColdTemp"),
                    Value4 = GetSlotValue("error4", "ColdTemp"),
                    Value5 = GetSlotValue("error5", "ColdTemp"),
                    Value6 = GetSlotValue("error6", "ColdTemp"),
                    Value7 = GetSlotValue("error7", "ColdTemp"),
                    Value8 = GetSlotValue("error8", "ColdTemp")
                });

                list.Add(new ErrorData
                {
                    Name = "ADC HOT",
                    Value = GetSlotValue("error1", "AdcHot"),
                    Value2 = GetSlotValue("error2", "AdcHot"),
                    Value3 = GetSlotValue("error3", "AdcHot"),
                    Value4 = GetSlotValue("error4", "AdcHot"),
                    Value5 = GetSlotValue("error5", "AdcHot"),
                    Value6 = GetSlotValue("error6", "AdcHot"),
                    Value7 = GetSlotValue("error7", "AdcHot"),
                    Value8 = GetSlotValue("error8", "AdcHot")
                });

                list.Add(new ErrorData
                {
                    Name = "ADC COLD",
                    Value = GetSlotValue("error1", "AdcCold"),
                    Value2 = GetSlotValue("error2", "AdcCold"),
                    Value3 = GetSlotValue("error3", "AdcCold"),
                    Value4 = GetSlotValue("error4", "AdcCold"),
                    Value5 = GetSlotValue("error5", "AdcCold"),
                    Value6 = GetSlotValue("error6", "AdcCold"),
                    Value7 = GetSlotValue("error7", "AdcCold"),
                    Value8 = GetSlotValue("error8", "AdcCold")
                });

                list.Add(new ErrorData
                {
                    Name = "초기급수 완료",
                    Value = GetSlotValue("error1", "WaterInitDone"),
                    Value2 = GetSlotValue("error2", "WaterInitDone"),
                    Value3 = GetSlotValue("error3", "WaterInitDone"),
                    Value4 = GetSlotValue("error4", "WaterInitDone"),
                    Value5 = GetSlotValue("error5", "WaterInitDone"),
                    Value6 = GetSlotValue("error6", "WaterInitDone"),
                    Value7 = GetSlotValue("error7", "WaterInitDone"),
                    Value8 = GetSlotValue("error8", "WaterInitDone")
                });

                list.Add(new ErrorData
                {
                    Name = "StatusA Raw",
                    Value = GetSlotValue("error1", "StatusA"),
                    Value2 = GetSlotValue("error2", "StatusA"),
                    Value3 = GetSlotValue("error3", "StatusA"),
                    Value4 = GetSlotValue("error4", "StatusA"),
                    Value5 = GetSlotValue("error5", "StatusA"),
                    Value6 = GetSlotValue("error6", "StatusA"),
                    Value7 = GetSlotValue("error7", "StatusA"),
                    Value8 = GetSlotValue("error8", "StatusA")
                });

                list.Add(new ErrorData
                {
                    Name = "StatusB Raw",
                    Value = GetSlotValue("error1", "StatusB"),
                    Value2 = GetSlotValue("error2", "StatusB"),
                    Value3 = GetSlotValue("error3", "StatusB"),
                    Value4 = GetSlotValue("error4", "StatusB"),
                    Value5 = GetSlotValue("error5", "StatusB"),
                    Value6 = GetSlotValue("error6", "StatusB"),
                    Value7 = GetSlotValue("error7", "StatusB"),
                    Value8 = GetSlotValue("error8", "StatusB")
                });

                list.Add(new ErrorData { Name = "A Heater", Value = GetStatusABitFromFile(1, 0x01), Value2 = GetStatusABitFromFile(2, 0x01), Value3 = GetStatusABitFromFile(3, 0x01), Value4 = GetStatusABitFromFile(4, 0x01), Value5 = GetStatusABitFromFile(5, 0x01), Value6 = GetStatusABitFromFile(6, 0x01), Value7 = GetStatusABitFromFile(7, 0x01), Value8 = GetStatusABitFromFile(8, 0x01) });
                list.Add(new ErrorData { Name = "A Compressor", Value = GetStatusABitFromFile(1, 0x02), Value2 = GetStatusABitFromFile(2, 0x02), Value3 = GetStatusABitFromFile(3, 0x02), Value4 = GetStatusABitFromFile(4, 0x02), Value5 = GetStatusABitFromFile(5, 0x02), Value6 = GetStatusABitFromFile(6, 0x02), Value7 = GetStatusABitFromFile(7, 0x02), Value8 = GetStatusABitFromFile(8, 0x02) });
                list.Add(new ErrorData { Name = "A HotValve", Value = GetStatusABitFromFile(1, 0x04), Value2 = GetStatusABitFromFile(2, 0x04), Value3 = GetStatusABitFromFile(3, 0x04), Value4 = GetStatusABitFromFile(4, 0x04), Value5 = GetStatusABitFromFile(5, 0x04), Value6 = GetStatusABitFromFile(6, 0x04), Value7 = GetStatusABitFromFile(7, 0x04), Value8 = GetStatusABitFromFile(8, 0x04) });
                list.Add(new ErrorData { Name = "A ColdSelect", Value = GetStatusABitFromFile(1, 0x08), Value2 = GetStatusABitFromFile(2, 0x08), Value3 = GetStatusABitFromFile(3, 0x08), Value4 = GetStatusABitFromFile(4, 0x08), Value5 = GetStatusABitFromFile(5, 0x08), Value6 = GetStatusABitFromFile(6, 0x08), Value7 = GetStatusABitFromFile(7, 0x08), Value8 = GetStatusABitFromFile(8, 0x08) });
                list.Add(new ErrorData { Name = "A OutletValve", Value = GetStatusABitFromFile(1, 0x10), Value2 = GetStatusABitFromFile(2, 0x10), Value3 = GetStatusABitFromFile(3, 0x10), Value4 = GetStatusABitFromFile(4, 0x10), Value5 = GetStatusABitFromFile(5, 0x10), Value6 = GetStatusABitFromFile(6, 0x10), Value7 = GetStatusABitFromFile(7, 0x10), Value8 = GetStatusABitFromFile(8, 0x10) });
                list.Add(new ErrorData { Name = "A PumpOutlet", Value = GetStatusABitFromFile(1, 0x20), Value2 = GetStatusABitFromFile(2, 0x20), Value3 = GetStatusABitFromFile(3, 0x20), Value4 = GetStatusABitFromFile(4, 0x20), Value5 = GetStatusABitFromFile(5, 0x20), Value6 = GetStatusABitFromFile(6, 0x20), Value7 = GetStatusABitFromFile(7, 0x20), Value8 = GetStatusABitFromFile(8, 0x20) });
                list.Add(new ErrorData { Name = "A PumpDiaphragm", Value = GetStatusABitFromFile(1, 0x40), Value2 = GetStatusABitFromFile(2, 0x40), Value3 = GetStatusABitFromFile(3, 0x40), Value4 = GetStatusABitFromFile(4, 0x40), Value5 = GetStatusABitFromFile(5, 0x40), Value6 = GetStatusABitFromFile(6, 0x40), Value7 = GetStatusABitFromFile(7, 0x40), Value8 = GetStatusABitFromFile(8, 0x40) });
                list.Add(new ErrorData { Name = "A PumpAirvent", Value = GetStatusABitFromFile(1, 0x80), Value2 = GetStatusABitFromFile(2, 0x80), Value3 = GetStatusABitFromFile(3, 0x80), Value4 = GetStatusABitFromFile(4, 0x80), Value5 = GetStatusABitFromFile(5, 0x80), Value6 = GetStatusABitFromFile(6, 0x80), Value7 = GetStatusABitFromFile(7, 0x80), Value8 = GetStatusABitFromFile(8, 0x80) });

                list.Add(new ErrorData { Name = "B FloatSensor", Value = GetStatusBBitFromFile(1, 0x01), Value2 = GetStatusBBitFromFile(2, 0x01), Value3 = GetStatusBBitFromFile(3, 0x01), Value4 = GetStatusBBitFromFile(4, 0x01), Value5 = GetStatusBBitFromFile(5, 0x01), Value6 = GetStatusBBitFromFile(6, 0x01), Value7 = GetStatusBBitFromFile(7, 0x01), Value8 = GetStatusBBitFromFile(8, 0x01) });
                list.Add(new ErrorData { Name = "B BallTop", Value = GetStatusBBitFromFile(1, 0x02), Value2 = GetStatusBBitFromFile(2, 0x02), Value3 = GetStatusBBitFromFile(3, 0x02), Value4 = GetStatusBBitFromFile(4, 0x02), Value5 = GetStatusBBitFromFile(5, 0x02), Value6 = GetStatusBBitFromFile(6, 0x02), Value7 = GetStatusBBitFromFile(7, 0x02), Value8 = GetStatusBBitFromFile(8, 0x02) });
                list.Add(new ErrorData { Name = "B WaterBuffer", Value = GetStatusBBitFromFile(1, 0x04), Value2 = GetStatusBBitFromFile(2, 0x04), Value3 = GetStatusBBitFromFile(3, 0x04), Value4 = GetStatusBBitFromFile(4, 0x04), Value5 = GetStatusBBitFromFile(5, 0x04), Value6 = GetStatusBBitFromFile(6, 0x04), Value7 = GetStatusBBitFromFile(7, 0x04), Value8 = GetStatusBBitFromFile(8, 0x04) });
                list.Add(new ErrorData { Name = "B EmptyDetect", Value = GetStatusBBitFromFile(1, 0x08), Value2 = GetStatusBBitFromFile(2, 0x08), Value3 = GetStatusBBitFromFile(3, 0x08), Value4 = GetStatusBBitFromFile(4, 0x08), Value5 = GetStatusBBitFromFile(5, 0x08), Value6 = GetStatusBBitFromFile(6, 0x08), Value7 = GetStatusBBitFromFile(7, 0x08), Value8 = GetStatusBBitFromFile(8, 0x08) });
                list.Add(new ErrorData { Name = "B BufferLow", Value = GetStatusBBitFromFile(1, 0x10), Value2 = GetStatusBBitFromFile(2, 0x10), Value3 = GetStatusBBitFromFile(3, 0x10), Value4 = GetStatusBBitFromFile(4, 0x10), Value5 = GetStatusBBitFromFile(5, 0x10), Value6 = GetStatusBBitFromFile(6, 0x10), Value7 = GetStatusBBitFromFile(7, 0x10), Value8 = GetStatusBBitFromFile(8, 0x10) });
                list.Add(new ErrorData { Name = "B ReheatRunning", Value = GetStatusBBitFromFile(1, 0x20), Value2 = GetStatusBBitFromFile(2, 0x20), Value3 = GetStatusBBitFromFile(3, 0x20), Value4 = GetStatusBBitFromFile(4, 0x20), Value5 = GetStatusBBitFromFile(5, 0x20), Value6 = GetStatusBBitFromFile(6, 0x20), Value7 = GetStatusBBitFromFile(7, 0x20), Value8 = GetStatusBBitFromFile(8, 0x20) });
                list.Add(new ErrorData { Name = "B HotIng", Value = GetStatusBBitFromFile(1, 0x40), Value2 = GetStatusBBitFromFile(2, 0x40), Value3 = GetStatusBBitFromFile(3, 0x40), Value4 = GetStatusBBitFromFile(4, 0x40), Value5 = GetStatusBBitFromFile(5, 0x40), Value6 = GetStatusBBitFromFile(6, 0x40), Value7 = GetStatusBBitFromFile(7, 0x40), Value8 = GetStatusBBitFromFile(8, 0x40) });
                list.Add(new ErrorData { Name = "B Dispensing", Value = GetStatusBBitFromFile(1, 0x80), Value2 = GetStatusBBitFromFile(2, 0x80), Value3 = GetStatusBBitFromFile(3, 0x80), Value4 = GetStatusBBitFromFile(4, 0x80), Value5 = GetStatusBBitFromFile(5, 0x80), Value6 = GetStatusBBitFromFile(6, 0x80), Value7 = GetStatusBBitFromFile(7, 0x80), Value8 = GetStatusBBitFromFile(8, 0x80) });

                list.Add(new ErrorData
                {
                    Name = "버퍼수위 부족",
                    Value = GetSlotValue("error1", "BufferLow"),
                    Value2 = GetSlotValue("error2", "BufferLow"),
                    Value3 = GetSlotValue("error3", "BufferLow"),
                    Value4 = GetSlotValue("error4", "BufferLow"),
                    Value5 = GetSlotValue("error5", "BufferLow"),
                    Value6 = GetSlotValue("error6", "BufferLow"),
                    Value7 = GetSlotValue("error7", "BufferLow"),
                    Value8 = GetSlotValue("error8", "BufferLow")
                });

                ErrorGrid.ItemsSource = list;
                ErrorFileName.Text = fileName;

                ToastMessage.ToastService.AppToast.Show("에러 파일을 불러왔습니다.");
            }
            catch (Exception ex)
            {
                log.Error("ErrorListDoubleClick 실패", ex);
                ToastMessage.ToastService.AppToast.Show("에러 파일을 불러오는 중 문제가 발생했습니다.");
            }
        }

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
            try
            {
                if (string.IsNullOrWhiteSpace(ErrorFileName.Text))
                {
                    ToastMessage.ToastService.AppToast.Show("에러 파일명을 입력하세요.");
                    return;
                }

                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string full = System.IO.Path.Combine(dir, ErrorFileName.Text.Trim() + ".config");

                XDocument doc = BuildErrorXmlFromGrid();
                if (doc == null)
                {
                    ToastMessage.ToastService.AppToast.Show("저장할 에러 데이터가 없습니다.");
                    return;
                }

                doc.Save(full);

                // [추가] 수동 저장 후 DB 적재
                if (_lastErrorResponse != null)
                {
                    MonitoringDbWriteService.Instance.EnqueueErrorHistory( _lastErrorResponse, BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE, _channelNoForDb );
                }

                SetErrorList();
                ClearErrorFileInputs();

                ToastMessage.ToastService.AppToast.Show("에러 데이터를 저장했습니다.");
            }
            catch (Exception ex)
            {
                log.Error("ErrorSaveButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("에러 저장 중 문제가 발생했습니다.");
            }
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
            SetList();
            SetErrorList();
            ClearParameterFileInputs();
            ClearErrorFileInputs();
            ToastMessage.ToastService.AppToast.Show("파일 목록을 새로고침했습니다.");
        }

        private void ErrorDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedCore(ErrorFileList.SelectedItems, hardDelete: false, kind: "Error");
            ClearErrorFileInputs();
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

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var paramRows = new ObservableCollection<StatusViewRow>();

                    paramRows.Add(new StatusViewRow() { Name = "Mode1 ON TIME CW", Value = onTimeCW1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode1 OFF TIME CW", Value = offTimeCW1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode1 ON TIME CCW", Value = onTimeCCW1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode1 OFF TIME CCW", Value = offTimeCCW1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode1 HEATER TEMP", Value = HeaterTemp1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode1 HEATER OFF TIME", Value = HeaterOffTime1.ToString() });

                    paramRows.Add(new StatusViewRow() { Name = "Mode2 ON TIME CW", Value = onTimeCW2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode2 OFF TIME CW", Value = offTimeCW2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode2 ON TIME CCW", Value = onTimeCCW2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode2 OFF TIME CCW", Value = offTimeCCW2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode2 HEATER TEMP", Value = HeaterTemp2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "Mode2 HEATER OFF TIME", Value = HeaterOffTime2.ToString() });

                    paramRows.Add(new StatusViewRow() { Name = "배기 FAN 대기 모드", Value = ExhaustFanWaitMode.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "배기 FAN 운전 모드", Value = ExhaustFanOperateMode.ToString() });

                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step1", Value = air_control1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step2", Value = air_control2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step3", Value = air_control3.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step4", Value = air_control4.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step5", Value = air_control5.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "열풍 온도 Step6", Value = air_control6.ToString() });

                    paramRows.Add(new StatusViewRow() { Name = "이물질 감지 시간", Value = motor1.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "이물질 감지 전류", Value = motor2.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "이물질 감지 횟수", Value = motor3.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "과부하 감지 전류", Value = motor4.ToString() });
                    paramRows.Add(new StatusViewRow() { Name = "과부하 감지 횟수", Value = motor5.ToString() });

                    _receivedRows = paramRows;
                    ReceivedParamGrid.ItemsSource = ToTwoColumnRows(_receivedRows);
                }));
            }));
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
            command[69] = 0x34;

            return command;
        }
        public void setError(byte[] data)
        {
            Duo8ErrorResponse resp = Duo8PacketParser.ParseErrorResponse(data);
            if (resp == null)
            {
                _isReadingError = false;
                if (ReadErrorButton != null)
                    ReadErrorButton.IsEnabled = true;
                return;
            }

            _lastErrorResponse = resp;  // 마지막으로 읽은 에러 응답을 저장

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

        // 자동 저장: ErrorGrid.ItemsSource를 XML로 저장 (파일명 충돌 방지 + 재시도 + 90일 보존)
        private void AutoSaveErrorDataToFile()
        {
            try
            {
                if (ErrorGrid == null)
                    return;

                XDocument doc = null;

                Dispatcher.Invoke(() =>
                {
                    doc = BuildErrorXmlFromGrid();
                });

                if (doc == null)
                    return;

                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                EnforceErrorDataRetention(dir);

                /* 아래 함수로 대체
                string baseName = "ErrorData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string full = System.IO.Path.Combine(dir, baseName + ".config");

                int suffix = 1;
                while (File.Exists(full))
                {
                    full = System.IO.Path.Combine(dir, $"{baseName}_click{suffix:00}.config");
                    suffix++;
                }
                */

                string full = BuildUniqueErrorFilePath(dir);
                doc.Save(full);

                // [추가] 자동 저장 후 DB 적재
                if (_lastErrorResponse != null)
                {
                    MonitoringDbWriteService.Instance.EnqueueErrorHistory( _lastErrorResponse, BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE, _channelNoForDb );
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetErrorList();
                }));
            }
            catch (Exception ex)
            {
                log.Error("AutoSaveErrorDataToFile 실패", ex);
            }
        }

        // ErrorData 보존 정책: 90일 초과 파일 삭제, 이어서 개수 상한(기본 10000) 초과 시 오래된 파일부터 정리
        private void EnforceErrorDataRetention(string directory)
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

        private void ReceivedParamGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
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
                win.SetInitialFilter(
                    sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE,
                    channelNo: _channelNoForDb
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

        private ObservableCollection<StatusViewRow2Col> ToTwoColumnRows(IEnumerable<StatusViewRow> source)
        {
            var result = new ObservableCollection<StatusViewRow2Col>();
            if (source == null) return result;

            var list = source.ToList();
            for (int i = 0; i < list.Count; i += 2)
            {
                var left = list[i];
                var right = (i + 1 < list.Count) ? list[i + 1] : null;

                result.Add(new StatusViewRow2Col
                {
                    Name1 = left?.Name ?? "",
                    Value1 = left?.Value ?? "",
                    Name2 = right?.Name ?? "",
                    Value2 = right?.Value ?? ""
                });
            }

            return result;
        }

        private void SaveParameterFile(string fileName)
        {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParameterSetting");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string full = System.IO.Path.Combine(dir, fileName + ".config");

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<configuration>");
            sb.AppendLine("  <Parameter>");

            int index = 1;
            foreach (var row in _editableRows)
            {
                string name = System.Security.SecurityElement.Escape(row.Name ?? "");
                string value = System.Security.SecurityElement.Escape((row.Value ?? "").Trim());

                sb.AppendLine($"    <add Name=\"{name}\" Value=\"{value}\" Index=\"{index}\" />");
                index++;
            }

            sb.AppendLine("  </Parameter>");
            sb.AppendLine("</configuration>");

            File.WriteAllText(full, sb.ToString(), Encoding.UTF8);
        }

        private void LoadParameterFile(string fileName)
        {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParameterSetting");
            string full = System.IO.Path.Combine(dir, fileName + ".config");

            if (!File.Exists(full))
                return;

            var doc = XDocument.Load(full);
            var section = doc.Root?.Element("Parameter");
            if (section == null)
            {
                ToastMessage.ToastService.AppToast.Show("파라미터 파일 형식이 올바르지 않습니다.");
                return;
            }

            _editableRows.Clear();

            foreach (var add in section.Elements("add"))
            {
                string name = add.Attribute("Name")?.Value ?? "";
                string value = add.Attribute("Value")?.Value ?? "";

                _editableRows.Add(new StatusViewRow
                {
                    Name = name,
                    Value = value
                });
            }

            EditableParamGrid.ItemsSource = null;
            EditableParamGrid.ItemsSource = ToTwoColumnRows(_editableRows);

            RightSet = true;
        }

        private void ClearErrorFileInputs()
        {
            if (ErrorFileName != null) ErrorFileName.Text = "";
            if (ErrorSearchBox != null) ErrorSearchBox.Text = "";
            if (ErrorFileList != null) ErrorFileList.SelectedItems.Clear();
            ApplyErrorFilter("");
        }

        private void ClearParameterFileInputs()
        {
            if (FileName != null) FileName.Text = "";
            if (FileSearchBox != null) FileSearchBox.Text = "";
            if (FileList != null) FileList.SelectedItems.Clear();
            ApplyFileFilter("");
        }

        private ErrorData FindErrorRow(ObservableCollection<ErrorData> rows, string name)
        {
            return rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private void SetErrorSlotValue(ErrorData row, int slot, string value)
        {
            if (row == null)
                return;

            switch (slot)
            {
                case 1: row.Value = value; break;
                case 2: row.Value2 = value; break;
                case 3: row.Value3 = value; break;
                case 4: row.Value4 = value; break;
                case 5: row.Value5 = value; break;
                case 6: row.Value6 = value; break;
                case 7: row.Value7 = value; break;
                case 8: row.Value8 = value; break;
            }
        }


        private ErrorData FindErrorRowByName(IEnumerable<ErrorData> rows, string name)
        {
            if (rows == null || string.IsNullOrWhiteSpace(name))
                return null;

            return rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private string GetErrorSlotValue(ErrorData row, int slot)
        {
            if (row == null) return "";

            switch (slot)
            {
                case 1: return row.Value ?? "";
                case 2: return row.Value2 ?? "";
                case 3: return row.Value3 ?? "";
                case 4: return row.Value4 ?? "";
                case 5: return row.Value5 ?? "";
                case 6: return row.Value6 ?? "";
                case 7: return row.Value7 ?? "";
                case 8: return row.Value8 ?? "";
                default: return "";
            }
        }

        private DocumentFormat.OpenXml.Spreadsheet.Cell CreateTextCell(string value)
        {
            return new DocumentFormat.OpenXml.Spreadsheet.Cell
            {
                DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.String,
                CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue(value ?? "")
            };
        }

        private void ErrorExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var src = ErrorGrid.ItemsSource as IEnumerable<ErrorData>;
                if (src == null)
                {
                    ToastMessage.ToastService.AppToast.Show("엑셀로 저장할 에러 데이터가 없습니다.");
                    return;
                }

                string defaultDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorData");
                if (!Directory.Exists(defaultDir))
                    Directory.CreateDirectory(defaultDir);

                string defaultFileName;
                if (!string.IsNullOrWhiteSpace(ErrorFileName.Text))
                    defaultFileName = ErrorFileName.Text.Trim() + ".xlsx";
                else
                    defaultFileName = "ErrorData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";

                SaveFileDialog dlg = new SaveFileDialog
                {
                    Title = "에러 엑셀 저장",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    AddExtension = true,
                    InitialDirectory = defaultDir,
                    FileName = defaultFileName,
                    OverwritePrompt = true
                };

                bool? result = dlg.ShowDialog(this);
                if (result != true)
                    return;

                string full = dlg.FileName;

                using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(
                    full,
                    DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                {
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();

                    var worksheetPart = workbookPart.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
                    var sheetData = new DocumentFormat.OpenXml.Spreadsheet.SheetData();

                    // 헤더
                    var headerRow = new DocumentFormat.OpenXml.Spreadsheet.Row();
                    headerRow.Append(
                        CreateTextCell("항목"),
                        CreateTextCell("Error1"),
                        CreateTextCell("Error2"),
                        CreateTextCell("Error3"),
                        CreateTextCell("Error4"),
                        CreateTextCell("Error5"),
                        CreateTextCell("Error6"),
                        CreateTextCell("Error7"),
                        CreateTextCell("Error8")
                    );
                    sheetData.Append(headerRow);

                    // 본문
                    foreach (var row in src)
                    {
                        var excelRow = new DocumentFormat.OpenXml.Spreadsheet.Row();
                        excelRow.Append(
                            CreateTextCell(row.Name ?? ""),
                            CreateTextCell(row.Value ?? ""),
                            CreateTextCell(row.Value2 ?? ""),
                            CreateTextCell(row.Value3 ?? ""),
                            CreateTextCell(row.Value4 ?? ""),
                            CreateTextCell(row.Value5 ?? ""),
                            CreateTextCell(row.Value6 ?? ""),
                            CreateTextCell(row.Value7 ?? ""),
                            CreateTextCell(row.Value8 ?? "")
                        );
                        sheetData.Append(excelRow);
                    }

                    worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(sheetData);

                    var sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
                    var sheet = new DocumentFormat.OpenXml.Spreadsheet.Sheet()
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "ErrorData"
                    };
                    sheets.Append(sheet);

                    workbookPart.Workbook.Save();
                }

                ToastMessage.ToastService.AppToast.Show("엑셀 파일을 저장했습니다.");
            }
            catch (Exception ex)
            {
                log.Error("ErrorExcelButton_Click 실패", ex);
                ToastMessage.ToastService.AppToast.Show("엑셀 저장 중 문제가 발생했습니다.");
            }
        }

        private ObservableCollection<ErrorData> BuildDefaultErrorRows()
        {
            return new ObservableCollection<ErrorData>
            {
                CreateDefaultErrorRow("유효", "N"),
                CreateDefaultErrorRow("SEQ", "0"),
                CreateDefaultErrorRow("에러코드", "0"),
                CreateDefaultErrorRow("에러명", "0"),
                CreateDefaultErrorRow("온수 Temp", "0"),
                CreateDefaultErrorRow("냉수 Temp", "0"),
                CreateDefaultErrorRow("ADC HOT", "0"),
                CreateDefaultErrorRow("ADC COLD", "0"),
                CreateDefaultErrorRow("초기급수 완료", "0"),

                CreateDefaultErrorRow("StatusA Raw", "0x00"),
                CreateDefaultErrorRow("StatusB Raw", "0x00"),

                CreateDefaultErrorRow("A Heater", "OFF"),
                CreateDefaultErrorRow("A Comp", "OFF"),
                CreateDefaultErrorRow("A HotValve", "OFF"),
                CreateDefaultErrorRow("A ColdSel", "OFF"),
                CreateDefaultErrorRow("A Outlet", "OFF"),
                CreateDefaultErrorRow("A PumpOut", "OFF"),
                CreateDefaultErrorRow("A PumpDia", "OFF"),
                CreateDefaultErrorRow("A Airvent", "OFF"),

                CreateDefaultErrorRow("B Float", "OFF"),
                CreateDefaultErrorRow("B BallTop", "OFF"),
                CreateDefaultErrorRow("B WaterBuf", "OFF"),
                CreateDefaultErrorRow("B Empty", "OFF"),
                CreateDefaultErrorRow("B BufLow", "OFF"),
                CreateDefaultErrorRow("B Reheat", "OFF"),
                CreateDefaultErrorRow("B HotIng", "OFF"),
                CreateDefaultErrorRow("B Disp", "OFF"),

                CreateDefaultErrorRow("버퍼수위 부족", "0")
            };
        }

        private ErrorData CreateDefaultErrorRow(string name, string value)
        {
            return new ErrorData
            {
                Name = name,
                Value = value,
                Value2 = value,
                Value3 = value,
                Value4 = value,
                Value5 = value,
                Value6 = value,
                Value7 = value,
                Value8 = value
            };
        }

        private void SetDefaultErrorGrid()
        {
            ErrorGrid.ItemsSource = BuildDefaultErrorRows();
        }

        private void SetDummyErrorGrid()
        {
            var rows = new ObservableCollection<ErrorData>
            {
                new ErrorData
                {
                    Name = "유효",
                    Value = "Y", Value2 = "Y", Value3 = "Y", Value4 = "N",
                    Value5 = "Y", Value6 = "N", Value7 = "Y", Value8 = "N"
                },
                new ErrorData
                {
                    Name = "SEQ",
                    Value = "1", Value2 = "2", Value3 = "3", Value4 = "4",
                    Value5 = "5", Value6 = "6", Value7 = "7", Value8 = "8"
                },
                new ErrorData
                {
                    Name = "에러코드",
                    Value = "0x01", Value2 = "0x02", Value3 = "0x03", Value4 = "0x04",
                    Value5 = "0x05", Value6 = "0x06", Value7 = "0x07", Value8 = "0x08"
                },
                new ErrorData
                {
                    Name = "에러명",
                    Value = "온수센서", Value2 = "냉수센서", Value3 = "버퍼수위", Value4 = "히터이상",
                    Value5 = "배기팬", Value6 = "밸브이상", Value7 = "과전류", Value8 = "테스트"
                },
                new ErrorData
                {
                    Name = "온수 Temp",
                    Value = "72", Value2 = "70", Value3 = "69", Value4 = "68",
                    Value5 = "67", Value6 = "66", Value7 = "65", Value8 = "64"
                },
                new ErrorData
                {
                    Name = "냉수 Temp",
                    Value = "14", Value2 = "15", Value3 = "16", Value4 = "17",
                    Value5 = "18", Value6 = "19", Value7 = "20", Value8 = "21"
                },
                new ErrorData
                {
                    Name = "ADC HOT",
                    Value = "410", Value2 = "412", Value3 = "414", Value4 = "416",
                    Value5 = "418", Value6 = "420", Value7 = "422", Value8 = "424"
                },
                new ErrorData
                {
                    Name = "ADC COLD",
                    Value = "210", Value2 = "212", Value3 = "214", Value4 = "216",
                    Value5 = "218", Value6 = "220", Value7 = "222", Value8 = "224"
                }
            };

            ErrorGrid.ItemsSource = rows;
        }

        private XDocument BuildErrorXmlFromGrid()
        {
            var src = ErrorGrid.ItemsSource as IEnumerable<ErrorData>;
            if (src == null)
                return null;

            var rows = src.ToList();

            XElement root = new XElement("ErrorData");

            for (int slot = 1; slot <= 8; slot++)
            {
                XElement errorNode = new XElement($"error{slot}",
                    new XElement("Valid", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "유효"), slot))),
                    new XElement("Seq", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "SEQ"), slot))),
                    new XElement("ErrorCode", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "에러코드"), slot))),
                    new XElement("ErrorName", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "에러명"), slot))),
                    new XElement("HotTemp", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "온수 Temp"), slot))),
                    new XElement("ColdTemp", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "냉수 Temp"), slot))),
                    new XElement("AdcHot", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "ADC HOT"), slot))),
                    new XElement("AdcCold", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "ADC COLD"), slot))),
                    new XElement("WaterInitDone", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "초기급수 완료"), slot))),
                    new XElement("StatusA", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "StatusA Raw"), slot))),
                    new XElement("StatusB", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "StatusB Raw"), slot))),
                    new XElement("BufferLow", NormalizeZeroForSave(GetErrorSlotValue(FindErrorRowByName(rows, "버퍼수위 부족"), slot)))
                );

                root.Add(errorNode);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        }

        private string GetErrorCodeText(byte code)
        {
            List<string> names = new List<string>();

            if ((code & 0x01) != 0) names.Add("COLD_ERR1");
            if ((code & 0x02) != 0) names.Add("COLD_ERR2");
            if ((code & 0x04) != 0) names.Add("HOT_ERR1");
            if ((code & 0x08) != 0) names.Add("HOT_ERR2");
            if ((code & 0x10) != 0) names.Add("HOT_ERR3");

            if (names.Count == 0)
                return "NONE";

            return string.Join(",", names);
        }


    }
}
