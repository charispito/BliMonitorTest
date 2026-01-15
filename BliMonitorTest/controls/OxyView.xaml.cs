using BliMonitorTest.data;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace BliMonitorTest.controls
{
    public partial class OxyView : UserControl
    {
        public MainViewModel ViewModel = new MainViewModel();
        private Timer timer;

        // ✅ 전체화면 창 1개만 관리
        private FullscreenPlotWindow _fullscreenWin;

        public OxyView()
        {
            InitializeComponent();

            this.DataContext = ViewModel;

            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;

            Loaded += OxyView_Loaded;
        }

        private void OxyView_Loaded(object sender, RoutedEventArgs e)
        {
            timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ViewModel != null)
                {
                    if (ViewModel.PlotModel != null)
                        ViewModel.PlotModel.InvalidatePlot(true);

                    if (ViewModel.FullscreenPlotModel != null)
                        ViewModel.FullscreenPlotModel.InvalidatePlot(true);
                }
            }));
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            ((MainViewModel)DataContext).Closing();
        }

        public void setLegend(int index, string label)
        {
            switch (index)
            {
                case 0: series1Legend.Content = label; break;
                case 1: series2Legend.Content = label; break;
                case 2: series3Legend.Content = label; break;
                case 3: series4Legend.Content = label; break;
                case 4: series5Legend.Content = label; break;
                case 5: series6Legend.Content = label; break;
                case 6: series7Legend.Content = label; break;
                case 7: series8Legend.Content = label; break;
            }
        }

        private void ResetAxes_Click(object sender, RoutedEventArgs e)
        {
            if (chart == null || chart.Model == null) return;

            chart.Model.ResetAllAxes();
            chart.InvalidatePlot(true);
        }

        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel vm = ViewModel;
            if (vm == null) return;
            if (vm.FullscreenPlotModel == null) return;

            // ✅ 이미 열려있으면 재사용
            if (_fullscreenWin != null)
            {
                _fullscreenWin.Activate();
                return;
            }

            _fullscreenWin = new FullscreenPlotWindow(vm.FullscreenPlotModel);
            _fullscreenWin.Owner = Window.GetWindow(this);

            // ✅ 닫히면 참조 제거 (다음 클릭에서 새로 열 수 있게)
            _fullscreenWin.Closed += (_, __) =>
            {
                _fullscreenWin = null;
            };

            // ShowDialog() 대신 Show()가 더 안전한 경우가 많습니다(타이머/이벤트 얽힘 줄어듦)
            _fullscreenWin.Show();
        }

        private void ClearChart_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "현재 차트 데이터를 모두 삭제하고 초기화할까요?\n(이 작업은 되돌릴 수 없습니다.)",
                "차트 초기화 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes) return;

            Window win = Window.GetWindow(this);
            BliMonitorTest.OneChannelWindow oneChannel = win as BliMonitorTest.OneChannelWindow;
            if (oneChannel != null)
            {
                oneChannel.ClearChartDataAndResetTime();
            }
        }

        private void BtnOpenDb_Click(object sender, RoutedEventArgs e)
        {
            var receiveDataQueryWindow = new ReceiveDataQueryWindow();
            receiveDataQueryWindow.Owner = Window.GetWindow(this);
            receiveDataQueryWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            receiveDataQueryWindow.Show();
        }
    }
}
