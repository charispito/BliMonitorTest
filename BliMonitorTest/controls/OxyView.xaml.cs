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
                        ViewModel.PlotModel.InvalidatePlot(false);

                    if (ViewModel.FullscreenPlotModel != null)
                        ViewModel.FullscreenPlotModel.InvalidatePlot(false);
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

            if (result != MessageBoxResult.Yes)
                return;

            // 1) 항상 현재 OxyView의 PlotModel/FullscreenPlotModel을 먼저 초기화
            ClearPlotModel(this.ViewModel);

            // 2) 창 타입별로 필요한 추가 초기화가 있으면 보강
            Window win = Window.GetWindow(this);
            if (win == null)
                return;

            // 단일채널: 시간/상태 리셋 등 창 전용 로직 실행
            BliMonitorTest.OneChannelWindow one = win as BliMonitorTest.OneChannelWindow;
            if (one != null)
            {
                // 창 전용 내부 버퍼/시간 등 리셋
                one.ClearChartDataAndResetTime();
                return;
            }
        }

        private void ClearPlotModel(BliMonitorTest.data.MainViewModel vm)
        {
            if (vm == null)
                return;

            // 메인 PlotModel
            OxyPlot.PlotModel model = vm.PlotModel;
            if (model != null)
            {
                if (model.Series != null)
                {
                    for (int i = 0; i < model.Series.Count; i++)
                    {
                        OxyPlot.Series.Series s = model.Series[i];

                        OxyPlot.Series.LineSeries ls = s as OxyPlot.Series.LineSeries;
                        if (ls != null)
                        {
                            if (ls.Points != null) ls.Points.Clear();
                            continue;
                        }

                        OxyPlot.Series.ScatterSeries ss = s as OxyPlot.Series.ScatterSeries;
                        if (ss != null)
                        {
                            if (ss.Points != null) ss.Points.Clear();
                            continue;
                        }

                        OxyPlot.Series.AreaSeries ars = s as OxyPlot.Series.AreaSeries;
                        if (ars != null)
                        {
                            if (ars.Points != null) ars.Points.Clear();
                            if (ars.Points2 != null) ars.Points2.Clear();
                            continue;
                        }

                        OxyPlot.Series.StemSeries sts = s as OxyPlot.Series.StemSeries;
                        if (sts != null)
                        {
                            if (sts.Points != null) sts.Points.Clear();
                            continue;
                        }
                    }
                }

                model.ResetAllAxes();

                if (chart != null)
                    chart.InvalidatePlot(true);
            }

            // 전체화면 PlotModel
            if (vm.FullscreenPlotModel != null)
            {
                OxyPlot.PlotModel f = vm.FullscreenPlotModel;

                if (f.Series != null)
                {
                    for (int i = 0; i < f.Series.Count; i++)
                    {
                        OxyPlot.Series.Series s = f.Series[i];

                        OxyPlot.Series.LineSeries ls = s as OxyPlot.Series.LineSeries;
                        if (ls != null)
                        {
                            if (ls.Points != null) ls.Points.Clear();
                            continue;
                        }

                        OxyPlot.Series.ScatterSeries ss = s as OxyPlot.Series.ScatterSeries;
                        if (ss != null)
                        {
                            if (ss.Points != null) ss.Points.Clear();
                            continue;
                        }

                        OxyPlot.Series.AreaSeries ars = s as OxyPlot.Series.AreaSeries;
                        if (ars != null)
                        {
                            if (ars.Points != null) ars.Points.Clear();
                            if (ars.Points2 != null) ars.Points2.Clear();
                            continue;
                        }
                    }
                }

                f.ResetAllAxes();
                vm.FullscreenPlotModel.InvalidatePlot(true);
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
