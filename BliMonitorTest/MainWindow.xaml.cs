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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BliMonitorTest
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            OneChannelButton.Click += OneChannelButton_Click;
            //멀티제품 - 미구현
            //MultiChannelButton.Click += MultiChannelButton_Click;
        }

        private void OneChannelButton_Click(object sender, RoutedEventArgs e)
        {
            new OneChannelWindow().Show();
            this.Close();
        }

        private void MultiChannelButton_Click(object sender, RoutedEventArgs e)
        {
            //멀티제품 - 미구현
            //new MultiWindow1().Show();
            this.Close();
        }
    }
}
