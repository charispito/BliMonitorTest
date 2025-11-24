using BliMonitorTest.data;
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
                case 0:
                    series1Legend.Content = label;
                    break;
                case 1:
                    series2Legend.Content = label;
                    break;
                case 2:
                    series3Legend.Content = label;
                    break;
                case 3:
                    series4Legend.Content = label;
                    break;
                case 4:
                    series5Legend.Content = label;
                    break;
                case 5:
                    series6Legend.Content = label;
                    break;
                case 6:
                    series7Legend.Content = label;
                    break;
                case 7:
                    series8Legend.Content = label;
                    break;
            }
        }
    }
}
