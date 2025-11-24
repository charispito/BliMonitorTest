using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BliMonitorTest.data
{
    public class MainViewModel : IDisposable
    {
        private bool disposed;
        private IList<LineSeries> series = new List<LineSeries>();

        public PlotModel PlotModel { get; private set; }

        public LineSeries LineSeries1 { get; private set; }
        public LineSeries LineSeries2 { get; private set; }
        public LineSeries LineSeries3 { get; private set; }
        public LineSeries LineSeries4 { get; private set; }
        public LineSeries LineSeries5 { get; private set; }
        public LineSeries LineSeries6 { get; private set; }
        public LineSeries LineSeries7 { get; private set; }
        public LineSeries LineSeries8 { get; private set; }
        private LinearAxis xAxis;


        public MainViewModel()
        {
            // Create a plot model
            this.PlotModel = new PlotModel();
            this.LineSeries1 = new LineSeries();
            this.LineSeries2 = new LineSeries();
            this.LineSeries3 = new LineSeries();
            this.LineSeries4 = new LineSeries();
            this.LineSeries5 = new LineSeries();
            this.LineSeries6 = new LineSeries();
            this.LineSeries7 = new LineSeries();
            this.LineSeries8 = new LineSeries();
            series.Add(this.LineSeries1);
            series.Add(this.LineSeries2);
            series.Add(this.LineSeries3);
            series.Add(this.LineSeries4);
            series.Add(this.LineSeries5);
            series.Add(this.LineSeries6);
            series.Add(this.LineSeries7);
            series.Add(this.LineSeries8);
            this.PlotModel.Series.Add(this.LineSeries1);
            this.PlotModel.Series.Add(this.LineSeries2);
            this.PlotModel.Series.Add(this.LineSeries3);
            this.PlotModel.Series.Add(this.LineSeries4);
            this.PlotModel.Series.Add(this.LineSeries5);
            this.PlotModel.Series.Add(this.LineSeries6);
            this.PlotModel.Series.Add(this.LineSeries7);
            this.PlotModel.Series.Add(this.LineSeries8);
            LineSeries1.Color = OxyColor.FromRgb(12, 34, 13);
            LineSeries2.Color = OxyColor.FromRgb(255, 34, 13);
            LinearAxis axis = new LinearAxis();
            axis.Key = "first";
            axis.Minimum = 0;
            axis.Maximum = 200;
            axis.IsPanEnabled = false;
            axis.IsZoomEnabled = false;
            axis.MinorGridlineThickness = 0;
            axis.MajorGridlineThickness = 0.8;
            axis.MajorGridlineColor = OxyColor.FromArgb(50, 0, 0, 0);
            axis.MajorGridlineStyle = LineStyle.Dash;
            axis.MinorTickSize = 0;
            axis.MajorTickSize = 0;
            axis.TextColor = OxyColor.FromRgb(255, 0, 0);
            axis.MajorStep = 20;
            axis.AxisDistance = 10;
            axis.Position = AxisPosition.Left;
            PlotModel.Axes.Add(axis);
            LinearAxis axis2 = new LinearAxis();
            axis2.Key = "second";
            axis2.Minimum = 0;
            axis2.IsPanEnabled = false;
            axis2.IsZoomEnabled = false;
            axis2.Maximum = 100;
            axis2.MajorStep = 10;
            axis2.AxisDistance = 42;
            axis2.MinorGridlineThickness = 0;
            axis2.TextColor = OxyColor.FromRgb(0, 0, 255);
            axis2.MajorTickSize = 0;
            axis2.MinorTickSize = 0;
            axis2.Position = AxisPosition.Left;
            PlotModel.Axes.Add(axis2);
            LinearAxis axis3 = new LinearAxis();
            axis3.Key = "third";
            axis3.Minimum = 0;
            axis3.Maximum = 2.0;
            axis3.TextColor = OxyColor.FromRgb(255, 0, 255);
            axis3.IsPanEnabled = false;
            axis3.IsZoomEnabled = false;
            axis3.AxisDistance = 66;
            axis3.MinorGridlineThickness = 0;
            axis3.StringFormat = "0.#";
            axis3.MajorTickSize = 0;
            axis3.MinorTickSize = 0;
            axis3.MajorStep = 0.2;
            axis3.Position = AxisPosition.Left;
            PlotModel.Axes.Add(axis3);
            xAxis = new LinearAxis();
            xAxis.Key = "time";
            xAxis.Minimum = 0;
            xAxis.Maximum = 140;
            //xAxis.MinorTickSize = 0;
            xAxis.MinorStep = 1;
            xAxis.MajorStep = 5;
            xAxis.StringFormat = "0분";
            xAxis.Position = AxisPosition.Bottom;
            xAxis.AbsoluteMinimum = 0;
            PlotModel.Axes.Add(xAxis);
        }

        public void initPan()
        {

        }

        public void setSeries(int index, int axeIndex, OxyColor color)
        {
            series[index].StrokeThickness = 1.3;
            series[index].Points.Clear();
            series[index].Color = color;
            series[index].XAxisKey = PlotModel.Axes[3].Key;
            series[index].YAxisKey = PlotModel.Axes[axeIndex].Key;
            series[index].Selectable = false;
            series[index].MinimumSegmentLength = 0.01;
        }

        public void unSetSeries(int index)
        {
            series[index].Points.Clear();
            //PlotModel.InvalidatePlot(true);
        }

        public void ClearPoints()
        {
            foreach(var s in series)
            {
                s.Points.Clear();
            }
            //PlotModel.InvalidatePlot(true);
        }

        public void ClearSeries(int index)
        {
            series[index].Points.Clear();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Closing()
        {
            // cancel the worker tasks
            Dispose();
        }

        public void panXAxis(double time)
        {
            double actualMax = PlotModel.Axes.Last().ActualMaximum;
            if (time > actualMax)
            {
                double panStep = xAxis.Transform(-1 + xAxis.Offset);
                Console.WriteLine("panStep: {0}", panStep);
                panStep = panStep / 54;
                xAxis.Pan(panStep);
            }
        }

        public void AddData(int index, DataPoint point)
        {
            series[index].Points.Add(point);            
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.Closing();
                }
            }

            this.disposed = true;
        }
    }
}
