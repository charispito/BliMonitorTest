using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// WPFChartView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WPFChartView : UserControl
    {
        public Timer timer = new Timer();
        private Random random = new Random();
        private DateTime dateX = DateTime.Now;
        private int yInterval = 20;
        private int xInterval = 10;
        public int _XMin = 0, _XMax = 120;
        public Dictionary<int, bool> ItemRun = new Dictionary<int, bool>();
        public List<LineSeries> seriesList = new List<LineSeries>();
        public List<LinearAxis> Yaxis = new List<LinearAxis>();
        private DateTimeAxis Time = new DateTimeAxis();
        public LinearAxis MinutesAxis = new LinearAxis();

        public WPFChartView(int channel)
        {
            InitializeComponent();
            Channel.Content = "Ch " + (channel + 1);
            chart.Background = Brushes.Transparent;

            LinearAxis axis = new LinearAxis();
            Style verticalStyle = new Style(typeof(NumericAxisLabel));
            verticalStyle.Setters.Add(new Setter(NumericAxisLabel.FontSizeProperty, 12.5));

            for (int i = 1; i < 5; i++)
            {
                ItemRun[i] = false;
            }

            axis.Orientation = AxisOrientation.Y;
            axis.Foreground = Brushes.Red;
            axis.ShowGridLines = true;
            axis.Minimum = 0;
            axis.Maximum = 200;
            axis.Location = AxisLocation.Auto;
            axis.Interval = 20;

            series1.DependentRangeAxis = axis;

            setStyle(series1, Brushes.Red);
            setStyle(series2, Brushes.Blue);
            setStyle(series3, new SolidColorBrush(Color.FromRgb(0xD0, 0xAF, 0x12)));
            setStyle(series4, Brushes.DeepSkyBlue);
            setStyle(series5, Brushes.Magenta);
            setStyle(series6, Brushes.SpringGreen);
            setStyle(series7, Brushes.Green);
            setStyle(series8, Brushes.Black);

            LinearAxis axis2 = new LinearAxis();
            axis2.ShowGridLines = false;
            axis2.Orientation = AxisOrientation.Y;
            axis2.Minimum = 0;
            axis2.Foreground = Brushes.Blue;
            axis2.Maximum = 100;
            axis2.Location = AxisLocation.Left;
            axis2.Interval = 10;
            HBar.ValueChanged += HBar_ValueChanged;

            series2.DependentRangeAxis = axis2;

            LinearAxis axis3 = new LinearAxis();
            axis3.Orientation = AxisOrientation.Y;
            axis3.Minimum = 0.0;
            axis3.Maximum = 2.0;
            axis3.Foreground = Brushes.Magenta;
            axis3.Location = AxisLocation.Left;
            axis3.Interval = 0.2;

            axis.AxisLabelStyle = verticalStyle;
            axis2.AxisLabelStyle = verticalStyle;
            axis3.AxisLabelStyle = verticalStyle;
            Yaxis.Add(axis);
            Yaxis.Add(axis2);
            Yaxis.Add(axis3);
            initXAxis();
        }

        private void HBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            Console.WriteLine("HBar : {0}", HBar.Value);
            if (MinutesAxis == null) return;
            double lMax = MinutesAxis.Maximum.Value;
            //if (lMax == null)
            //    lMax = MinutesAxis.ActualMaximum;
            double lZoom = (_XMax - _XMin) * 50 / 100.0 / 2.0;
            double lValue = HBar.Value;
            Console.WriteLine("lMax : {0} lValue: {1} lZoom: {2}", lMax, lValue, lZoom);
            if (lValue > lMax)
            {
                MinutesAxis.Maximum = Math.Max(lValue + lZoom, _XMax);
                MinutesAxis.Minimum = Math.Min(MinutesAxis.Maximum.Value - lZoom, _XMin);
                return;
            }
            MinutesAxis.Minimum = Math.Max(lValue - lZoom, _XMin);          // widen
            MinutesAxis.Maximum = Math.Min(MinutesAxis.Minimum.Value + lZoom, _XMax);          // tighten
            e.Handled = true;
        }

        public void setAxis(LineSeries series, int index)
        {
            series.DependentRangeAxis = Yaxis[index];
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

        public void initXAxis()
        {
            

            bool run = false;
            for (int i = 1; i < 5; i++)
            {
                if (ItemRun[i])
                    run = true;
            }
            if (run)
                return;
            Time = new DateTimeAxis();

            MinutesAxis = new LinearAxis();
            MinutesAxis.Orientation = AxisOrientation.X;
            MinutesAxis.Interval = 5;
            Style linearStyle = new Style();
            Style gridStyle = new Style();
            MinutesAxis.ShowGridLines = true;
            MinutesAxis.GridLineStyle = gridStyle;
            linearStyle.Setters.Add(new Setter(NumericAxisLabel.FontSizeProperty, 10.0));
            linearStyle.Setters.Add(new Setter(NumericAxisLabel.StringFormatProperty, "{0: 0분}"));
            MinutesAxis.AxisLabelStyle = linearStyle;
            MinutesAxis.Minimum = 0;
            MinutesAxis.Maximum = 120;
            //Style axisStyle = new Style(typeof(DateTimeAxisLabel));
            //axisStyle.Setters.Add(new Setter(DateTimeAxisLabel.FontSizeProperty, 10.0));
            //axisStyle.Setters.Add(new Setter(DateTimeAxisLabel.StringFormatProperty, "{0: mm분}"));
            //Time.Interval = xInterval;
            //Time.Orientation = AxisOrientation.X;
            //Time.IntervalType = DateTimeIntervalType.Minutes;
            //Time.Location = AxisLocation.Bottom;
            //Time.Minimum = DateTime.Now;
            //Time.Maximum = DateTime.Now.AddMinutes(180);
            //Time.AxisLabelStyle = axisStyle;
            series1.IndependentAxis = MinutesAxis;
            series2.IndependentAxis = MinutesAxis;
            series3.IndependentAxis = MinutesAxis;
            series4.IndependentAxis = MinutesAxis;
            series5.IndependentAxis = MinutesAxis;
            series6.IndependentAxis = MinutesAxis;
            series7.IndependentAxis = MinutesAxis;
            series8.IndependentAxis = MinutesAxis;
        }

        public void setViewPort(double value)
        {
            //var max = HBar.Maximum - HBar.Minimum;
            //HBar.ViewportSize = value / (max - value) * max;
        }

        public WPFChartView()
        {
            InitializeComponent();
            chart.Background = Brushes.Transparent;
            setViewPort(99.99);
            LinearAxis axis = new LinearAxis();
            Style verticalStyle = new Style(typeof(NumericAxisLabel));
            verticalStyle.Setters.Add(new Setter(NumericAxisLabel.FontSizeProperty, 12.5));
            for (int i = 1; i < 5; i++)
            {
                ItemRun[i] = false;
            }
            HBar.ValueChanged += HBar_ValueChanged;
            axis.Orientation = AxisOrientation.Y;
            axis.Foreground = Brushes.Red;
            axis.Minimum = 0;
            axis.Maximum = 200;
            axis.Location = AxisLocation.Left;
            axis.Interval = 20;
            series1.DependentRangeAxis = axis;
            setStyle(series1, Brushes.Red);
            setStyle(series2, Brushes.Blue);
            setStyle(series3, new SolidColorBrush(Color.FromRgb(0xD0, 0xAF, 0x12)));
            setStyle(series4, Brushes.DeepSkyBlue);
            setStyle(series5, Brushes.Magenta);
            setStyle(series6, Brushes.SpringGreen);
            setStyle(series7, Brushes.Green);
            setStyle(series8, Brushes.Black);


            LinearAxis axis2 = new LinearAxis();
            axis2.ShowGridLines = false;
            axis2.Orientation = AxisOrientation.Y;
            axis2.Minimum = 0;
            axis2.Foreground = Brushes.Blue;
            axis2.Maximum = 100;
            axis2.Location = AxisLocation.Left;
            axis2.Interval = 10;
            series2.DependentRangeAxis = axis2;
            LinearAxis axis3 = new LinearAxis();
            axis3.Orientation = AxisOrientation.Y;
            axis3.Minimum = 0.0;
            axis3.Maximum = 2.0;
            axis3.Foreground = Brushes.Green;
            axis3.Location = AxisLocation.Left;
            axis3.Interval = 0.2;

            axis.AxisLabelStyle = verticalStyle;
            axis2.AxisLabelStyle = verticalStyle;
            axis3.AxisLabelStyle = verticalStyle;
            Yaxis.Add(axis);
            Yaxis.Add(axis2);
            Yaxis.Add(axis3);
            seriesList.Add(series1);
            seriesList.Add(series2);
            seriesList.Add(series3);
            seriesList.Add(series4);
            seriesList.Add(series5);
            seriesList.Add(series6);
            seriesList.Add(series7);
            seriesList.Add(series8);
        }

        private void setStyle(LineSeries series, SolidColorBrush color)
        {
            Style style = new Style(typeof(LineDataPoint));
            style.Setters.Add(new Setter(LineDataPoint.TemplateProperty, null));
            style.Setters.Add(new Setter(LineDataPoint.BackgroundProperty, color));
            series.DataPointStyle = style;
            Style polyStyle = new Style(typeof (Polyline));
            polyStyle.Setters.Add(new Setter(Polyline.StrokeThicknessProperty, 1.3d));
            series.PolylineStyle = polyStyle;
        }
    }
}