using System.Windows;
using System.Windows.Input;
using OxyPlot;

namespace BliMonitorTest.controls
{
    public partial class FullscreenPlotWindow : Window
    {
        public FullscreenPlotWindow(PlotModel model)
        {
            InitializeComponent();
            DataContext = model;
            Focusable = true;
            Loaded += (_, __) => Keyboard.Focus(this);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
