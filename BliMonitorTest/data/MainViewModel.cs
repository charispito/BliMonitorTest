using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
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
            axis3.LabelFormatter = v => string.Format("{0:0.#}", v);
            //axis3.StringFormat = "0.#";
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
            xAxis.LabelFormatter = v => string.Format("{0}분", (int)v);
            //xAxis.StringFormat = "0분";
            xAxis.Position = AxisPosition.Bottom;
            xAxis.AbsoluteMinimum = 0;
            PlotModel.Axes.Add(xAxis);

            ApplyChartTheme();
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

        private void ApplyChartTheme()
        {
            // 1) 전체 기본 톤 (배경/폰트/테두리)
            PlotModel.Background = OxyColor.FromRgb(250, 250, 252);            // 컨트롤 배경
            PlotModel.PlotAreaBackground = OxyColor.FromRgb(255, 255, 255);    // 플롯 영역 배경
            PlotModel.PlotAreaBorderColor = OxyColor.FromRgb(220, 220, 230);
            PlotModel.PlotAreaBorderThickness = new OxyThickness(1);

            PlotModel.DefaultFont = "Segoe UI";
            PlotModel.DefaultFontSize = 11;
            PlotModel.TextColor = OxyColor.FromRgb(40, 40, 55);
            PlotModel.TitleColor = OxyColor.FromRgb(40, 40, 55);

            // ✅ Legend 객체 방식 (현재 프로젝트의 OxyPlot API와 일치)
            var legend = new Legend
            {
                // 위치/배치
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal,

                // 스타일
                LegendBackground = OxyColor.FromAColor(210, OxyColors.White),
                LegendBorder = OxyColor.FromRgb(220, 220, 230),
                LegendBorderThickness = 1,

                LegendFont = "Segoe UI",
                LegendFontSize = 11,

                // 여백 조금 주면 더 깔끔
                LegendPadding = 6,
                LegendItemSpacing = 10
            };

            // 3) 축 스타일 통일(가독성 개선)
            for (int i = 0; i < PlotModel.Axes.Count; i++)
            {
                Axis a = PlotModel.Axes[i];
                ApplyAxisTheme(a);
            }

            // 4) 라인 시리즈 스타일 통일(두께/마커/안티앨리어싱 느낌)
            //    색은 기존에 이미 지정한 걸 존중하고, '선만' 다듬음
            foreach (var s in series)
            {
                ApplyLineTheme(s);
            }

            PlotModel.InvalidatePlot(false);
        }

        private void ApplyAxisTheme(Axis axis)
        {
            // 축 라인/눈금
            axis.AxislineColor = OxyColor.FromRgb(160, 160, 175);
            axis.AxislineThickness = 1;
            axis.TicklineColor = OxyColor.FromRgb(160, 160, 175);

            // 그리드: "연한 실선"이 가장 깔끔하고 현대적으로 보임
            axis.MajorGridlineStyle = LineStyle.Solid;
            axis.MajorGridlineColor = OxyColor.FromRgb(235, 235, 242);
            axis.MajorGridlineThickness = 1;

            axis.MinorGridlineStyle = LineStyle.None;  // 화면이 복잡해지면 Minor는 끄는 게 대체로 예쁨

            // 글자
            axis.TextColor = axis.TextColor.IsUndefined() ? OxyColor.FromRgb(60, 60, 80) : axis.TextColor;
            axis.TitleColor = OxyColor.FromRgb(60, 60, 80);
            axis.FontSize = 11;
            axis.TitleFontSize = 12;

            // 축 바깥 여백(너무 붙어 보이면 답답함)
            if (axis.AxisDistance < 6)
            {
                axis.AxisDistance = 8;
            }

            // 기존 코드에서 TickSize를 0으로 다 꺼놨는데,
            // 아주 작게라도 주면 “차트 툴 느낌”이 좋아짐 (원치 않으면 주석 처리)
            if (axis.MajorTickSize == 0) axis.MajorTickSize = 3;
            if (axis.MinorTickSize == 0) axis.MinorTickSize = 0; // minor는 계속 0
        }

        private void ApplyLineTheme(LineSeries s)
        {
            // 굵기: 1.3은 살짝 얇은 편이라 1.6~2.0 사이가 보기 좋음
            if (s.StrokeThickness < 1.6)
            {
                s.StrokeThickness = 1.8;
            }

            // 데이터 포인트가 많을 때 마커는 보통 꺼두는 게 예쁨 + 성능 좋음
            s.MarkerType = MarkerType.None;

            // 선이 꺾여 보이면 이 값이 도움되는 경우가 있음(이미 사용 중이면 유지)
            if (s.MinimumSegmentLength <= 0)
            {
                s.MinimumSegmentLength = 0.01;
            }
        }

    }
}
