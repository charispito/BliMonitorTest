using System;
using System.Collections.Generic;
using System.Drawing;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// ChartView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChartView : UserControl
    {
        public double dateX = DateTime.MinValue.ToOADate();
        public List<Series> seriesList = new List<Series>();
        public Timer timer = new Timer();
        public Random random = new Random();
        public Chart chart = new Chart();
        public Legend legend1 = new Legend();
        public ChartArea chartArea1 = new ChartArea();
        public ChartView()
        {
            InitializeComponent();
            Channel.Visibility = System.Windows.Visibility.Collapsed;
            graphInit();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;
        }
        public ChartView(int channel)
        {
            InitializeComponent();
            Channel.Content = "ch " + channel;
            graphInit();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => 
            {
                for(int i = 0; i < seriesList.Count; i++)
                {
                    seriesList[i].Points.AddXY(dateX, random.Next(90 * (i + 1), 100 * (i + 1)));
                }
                dateX = DateTime.FromOADate(dateX).AddSeconds(1).ToOADate();
            }));
        }

        private void graphInit()
        {
            DateTime dt0 = DateTime.MinValue;
            chartArea1.Area3DStyle.Inclination = 15;
            chartArea1.Area3DStyle.IsClustered = true;
            chartArea1.Area3DStyle.IsRightAngleAxes = false;
            chartArea1.Area3DStyle.Perspective = 10;
            chartArea1.Area3DStyle.Rotation = 10;
            chartArea1.Area3DStyle.WallWidth = 0;
            chartArea1.AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount;
            chartArea1.AxisX.IsLabelAutoFit = false;
            chartArea1.AxisX.LabelStyle.Font = new System.Drawing.Font("Trebuchet MS", 7F, System.Drawing.FontStyle.Bold);
            chartArea1.AxisX.LabelStyle.Format = "HH:mm";
            chartArea1.AxisX.ScrollBar.IsPositionedInside = true;
            chartArea1.AxisX.Interval = 5;
            chartArea1.AxisX.Minimum = DateTime.MinValue.AddSeconds(0).ToOADate();
            chartArea1.AxisX.Maximum = DateTime.MinValue.AddSeconds(1800).ToOADate();
            chartArea1.AxisX.ScrollBar.Enabled = true;
            chartArea1.CursorX.AutoScroll = true;
            chartArea1.AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;
            chartArea1.AxisX.ScaleView.SmallScrollSize = 10;
            chartArea1.AxisX.IntervalType = DateTimeIntervalType.Minutes;
            chartArea1.AxisX.LineColor = Color.FromArgb(64, 64, 64, 64);
            chartArea1.AxisX.MajorGrid.LineColor = Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            chartArea1.AxisX.MajorTickMark.Size = 2F;
            chartArea1.AxisY.IsLabelAutoFit = false;
            chartArea1.AxisY.IsStartedFromZero = false;
            chartArea1.AxisY.LabelStyle.Font = new Font("Trebuchet MS", 8.25F, System.Drawing.FontStyle.Bold);
            chartArea1.AxisY.LabelStyle.Format = "N0";
            chartArea1.AxisY.LineColor = Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            chartArea1.AxisY.MajorGrid.LineColor = Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            chartArea1.BackColor = Color.White;
            chartArea1.BackSecondaryColor = Color.White;
            chartArea1.BorderColor = Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            chartArea1.Name = "Default";
            chartArea1.ShadowColor = Color.Transparent;
            chart.ChartAreas.Add(chartArea1);
            legend1.BackColor = Color.Transparent;
            legend1.Enabled = false;
            legend1.Font = new Font("Trebuchet MS", 8.25F, System.Drawing.FontStyle.Bold);
            legend1.IsTextAutoFit = false;
            legend1.Name = "Default";
            chart.Legends.Add(legend1);
            chart.Name = "Chart";
            setSeries(new Series(), Color.Red, 1);
            setSeries(new Series(), Color.Blue, 2);
            setSeries(new Series(), Color.Yellow, 3);
            setSeries(new Series(), Color.LightBlue, 4);
            setSeries(new Series(), Color.HotPink, 5);
            setSeries(new Series(), Color.LightGreen, 6);
            setSeries(new Series(), Color.Green, 7);
            setSeries(new Series(), Color.Black, 8);
            chart.TabIndex = 1;
            Host.Child = chart;
        }

        private void setSeries(Series series, Color color, int index)
        {
            series.Color = color;
            series.BorderWidth = 2;
            series.ChartArea = "Default";
            series.ChartType = SeriesChartType.Line;
            series.XValueType = ChartValueType.DateTime;
            series.Legend = "Default";
            series.Name = "Series" + index;
            seriesList.Add(series);
            chart.Series.Add(series);            
        }
    }
}
