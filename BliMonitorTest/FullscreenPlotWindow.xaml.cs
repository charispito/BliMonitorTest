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

            FullscreenChart.Model = model;

            // ✅ 닫힐 때 PlotView ↔ PlotModel 연결 끊기 (재오픈 크래시 방지에 효과적)
            Closed += (_, __) =>
            {
                FullscreenChart.Model = null;
            };

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
