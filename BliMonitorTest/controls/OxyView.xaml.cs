using BliMonitorTest.data;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// OxyView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OxyView : UserControl
    {
        public MainViewModel ViewModel = new MainViewModel();
        private Timer timer;
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
            Dispatcher.BeginInvoke(new Action(() => {
                ViewModel.PlotModel.InvalidatePlot(true);
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
            if (vm == null || vm.PlotModel == null) return;

            PlotModel src = vm.PlotModel;
            PlotModel cloned = PlotModelCloneHelper.CloneForView(src);

            var win = new FullscreenPlotWindow(cloned);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
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
            {
                return;
            }

            // ✅ OneChannelWindow 찾아서 초기화 + 더미시간 리셋 호출
            Window win = Window.GetWindow(this);
            BliMonitorTest.OneChannelWindow oneChannel = win as BliMonitorTest.OneChannelWindow;
            if (oneChannel != null)
            {
                // private이면 호출 불가 -> 아래 3)처럼 접근 레벨만 조정하면 됨
                oneChannel.ClearChartDataAndResetTime();
            }
        }

    }
}
