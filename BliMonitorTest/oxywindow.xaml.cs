using BliMonitorTest.data;
using System;
using System.Collections.Generic;
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
    /// oxywindow.xaml에 대한 상호 작용 논리
    /// </summary>
    [Example("Updating a LineSeries from a Task running on the UI thread synchronization context.")]
    public partial class oxywindow : Window
    {
        public oxywindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
            Closing += Oxywindow_Closing;
        }

        private void Oxywindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((MainViewModel)DataContext).Closing();
        }
    }
}
