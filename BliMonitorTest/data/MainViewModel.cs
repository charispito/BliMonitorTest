using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BliMonitorTest.data
{
    public class MainViewModel : IDisposable
    {
        private bool disposed;

        // === 메인용 ===
        private readonly IList<LineSeries> _seriesMain = new List<LineSeries>();
        private LinearAxis _xAxisMain;

        public PlotModel PlotModel { get; private set; }

        public LineSeries LineSeries1 { get; private set; }
        public LineSeries LineSeries2 { get; private set; }
        public LineSeries LineSeries3 { get; private set; }
        public LineSeries LineSeries4 { get; private set; }
        public LineSeries LineSeries5 { get; private set; }
        public LineSeries LineSeries6 { get; private set; }
        public LineSeries LineSeries7 { get; private set; }
        public LineSeries LineSeries8 { get; private set; }

        // === 전체화면용(신규) ===
        private readonly IList<LineSeries> _seriesFullscreen = new List<LineSeries>();
        private LinearAxis _xAxisFullscreen;

        public PlotModel FullscreenPlotModel { get; private set; }

        public MainViewModel()
        {
            // 1) 메인 모델 생성
            PlotModel = CreatePlotModel();
            (_xAxisMain, LineSeries1, LineSeries2, LineSeries3, LineSeries4, LineSeries5, LineSeries6, LineSeries7, LineSeries8) =
                CreateAxesAndSeriesAndAttach(PlotModel, _seriesMain);

            ApplyChartTheme(PlotModel, _seriesMain);

            // 2) 전체화면 모델 생성(완전히 별도 인스턴스)
            FullscreenPlotModel = CreatePlotModel();
            (_xAxisFullscreen, _, _, _, _, _, _, _, _) =
                CreateAxesAndSeriesAndAttach(FullscreenPlotModel, _seriesFullscreen);

            ApplyChartTheme(FullscreenPlotModel, _seriesFullscreen);
        }

        // --------------------------------------------------------------------
        // 기존 코드에서 호출하던 메서드들: 내부에서 "메인 + 전체화면" 둘 다 갱신
        // --------------------------------------------------------------------

        public void initPan()
        {
            // 필요 시 구현
        }

        public void setSeries(int index, int axeIndex, OxyColor color)
        {
            // 메인 적용
            ApplySeriesSetting(PlotModel, _seriesMain, index, axeIndex, color);

            // 전체화면도 동일 적용
            ApplySeriesSetting(FullscreenPlotModel, _seriesFullscreen, index, axeIndex, color);
        }

        public void unSetSeries(int index)
        {
            if (index < 0 || index >= _seriesMain.Count) return;

            _seriesMain[index].Points.Clear();
            _seriesFullscreen[index].Points.Clear();
        }

        public void ClearPoints()
        {
            foreach (var s in _seriesMain) s.Points.Clear();
            foreach (var s in _seriesFullscreen) s.Points.Clear();
        }

        public void ClearSeries(int index)
        {
            if (index < 0 || index >= _seriesMain.Count) return;

            _seriesMain[index].Points.Clear();
            _seriesFullscreen[index].Points.Clear();
        }

        public void panXAxis(double time)
        {
            // 메인
            PanXAxisInternal(PlotModel, _xAxisMain, time);

            // 전체화면
            PanXAxisInternal(FullscreenPlotModel, _xAxisFullscreen, time);
        }

        public void AddData(int index, DataPoint point)
        {
            if (index < 0 || index >= _seriesMain.Count) return;

            // 메인 + 전체화면에 동일 포인트 추가
            _seriesMain[index].Points.Add(point);
            _seriesFullscreen[index].Points.Add(point);
        }

        // 필요하다면 기존 로직처럼 외부에서 호출하게 두셔도 됩니다.
        public void InvalidateBoth(bool updateData = true)
        {
            PlotModel?.InvalidatePlot(updateData);
            FullscreenPlotModel?.InvalidatePlot(updateData);
        }

        // --------------------------------------------------------------------
        // IDisposable
        // --------------------------------------------------------------------

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Closing()
        {
            Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;
        }

        // --------------------------------------------------------------------
        // 내부 헬퍼들(생성/스타일/팬)
        // --------------------------------------------------------------------

        private static PlotModel CreatePlotModel()
        {
            return new PlotModel();
        }

        private static (LinearAxis xAxis,
                        LineSeries s1, LineSeries s2, LineSeries s3, LineSeries s4,
                        LineSeries s5, LineSeries s6, LineSeries s7, LineSeries s8)
            CreateAxesAndSeriesAndAttach(PlotModel model, IList<LineSeries> targetSeriesList)
        {
            // Series 8개 생성
            var s1 = new LineSeries();
            var s2 = new LineSeries();
            var s3 = new LineSeries();
            var s4 = new LineSeries();
            var s5 = new LineSeries();
            var s6 = new LineSeries();
            var s7 = new LineSeries();
            var s8 = new LineSeries();

            targetSeriesList.Add(s1);
            targetSeriesList.Add(s2);
            targetSeriesList.Add(s3);
            targetSeriesList.Add(s4);
            targetSeriesList.Add(s5);
            targetSeriesList.Add(s6);
            targetSeriesList.Add(s7);
            targetSeriesList.Add(s8);

            // PlotModel.Series에 추가
            model.Series.Add(s1);
            model.Series.Add(s2);
            model.Series.Add(s3);
            model.Series.Add(s4);
            model.Series.Add(s5);
            model.Series.Add(s6);
            model.Series.Add(s7);
            model.Series.Add(s8);

            // (사용자 코드 일부 반영) 기본 색 예시
            s1.Color = OxyColor.FromRgb(12, 34, 13);
            s2.Color = OxyColor.FromRgb(255, 34, 13);

            // Y축들(원본 코드의 Key/범위 일부를 그대로 옮김)
            var axis1 = new LinearAxis
            {
                Key = "first",
                Minimum = 0,
                Maximum = 200,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MinorGridlineThickness = 0,
                MajorGridlineThickness = 0.8,
                MajorGridlineColor = OxyColor.FromArgb(50, 0, 0, 0),
                MajorGridlineStyle = LineStyle.Dash,
                MinorTickSize = 0,
                MajorTickSize = 0,
                TextColor = OxyColor.FromRgb(255, 0, 0),
                MajorStep = 20,
                AxisDistance = 10,
                Position = AxisPosition.Left
            };
            model.Axes.Add(axis1);

            var axis2 = new LinearAxis
            {
                Key = "second",
                Minimum = 0,
                Maximum = 100,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorStep = 10,
                AxisDistance = 42,
                MinorGridlineThickness = 0,
                TextColor = OxyColor.FromRgb(0, 0, 255),
                MajorTickSize = 0,
                MinorTickSize = 0,
                Position = AxisPosition.Left
            };
            model.Axes.Add(axis2);

            var axis3 = new LinearAxis
            {
                Key = "third",
                Minimum = 0,
                Maximum = 2.0,
                TextColor = OxyColor.FromRgb(255, 0, 255),
                IsPanEnabled = false,
                IsZoomEnabled = false,
                AxisDistance = 66,
                MinorGridlineThickness = 0,
                LabelFormatter = v => string.Format("{0:0.#}", v),
                MajorTickSize = 0,
                MinorTickSize = 0,
                MajorStep = 0.2,
                Position = AxisPosition.Left
            };
            model.Axes.Add(axis3);

            // X축(time)
            var xAxis = new LinearAxis
            {
                Key = "time",
                Minimum = 0,
                Maximum = 140,
                MinorStep = 1,
                MajorStep = 5,
                LabelFormatter = v => string.Format("{0}분", (int)v),
                Position = AxisPosition.Bottom,
                AbsoluteMinimum = 0
            };
            model.Axes.Add(xAxis);

            return (xAxis, s1, s2, s3, s4, s5, s6, s7, s8);
        }

        private static void ApplySeriesSetting(PlotModel model, IList<LineSeries> series, int index, int axeIndex, OxyColor color)
        {
            if (model == null) return;
            if (index < 0 || index >= series.Count) return;
            if (axeIndex < 0 || axeIndex >= model.Axes.Count) return;

            series[index].StrokeThickness = 1.3;
            series[index].Points.Clear();
            series[index].Color = color;

            // 원본 코드: X축은 Axes[3], Y축은 axeIndex
            if (model.Axes.Count > 3)
                series[index].XAxisKey = model.Axes[3].Key;

            series[index].YAxisKey = model.Axes[axeIndex].Key;

            series[index].Selectable = false;
            series[index].MinimumSegmentLength = 0.01;
        }

        private static void PanXAxisInternal(PlotModel model, LinearAxis xAxis, double time)
        {
            if (model == null || xAxis == null) return;

            double actualMax = model.Axes.Last().ActualMaximum;
            if (time > actualMax)
            {
                double panStep = xAxis.Transform(-1 + xAxis.Offset);
                panStep = panStep / 54;
                xAxis.Pan(panStep);
            }
        }

        private static void ApplyChartTheme(PlotModel model, IEnumerable<LineSeries> series)
        {
            // 1) 기본 톤
            model.Background = OxyColor.FromRgb(250, 250, 252);
            model.PlotAreaBackground = OxyColor.FromRgb(255, 255, 255);
            model.PlotAreaBorderColor = OxyColor.FromRgb(220, 220, 230);
            model.PlotAreaBorderThickness = new OxyThickness(1);

            model.DefaultFont = "Segoe UI";
            model.DefaultFontSize = 11;
            model.TextColor = OxyColor.FromRgb(40, 40, 55);
            model.TitleColor = OxyColor.FromRgb(40, 40, 55);

            // 2) Legend
            model.Legends.Clear();
            model.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendBackground = OxyColor.FromAColor(210, OxyColors.White),
                LegendBorder = OxyColor.FromRgb(220, 220, 230),
                LegendBorderThickness = 1,
                LegendFont = "Segoe UI",
                LegendFontSize = 11,
                LegendPadding = 6,
                LegendItemSpacing = 10
            });

            // 3) 축 통일
            foreach (var a in model.Axes)
                ApplyAxisTheme(a);

            // 4) 라인 시리즈 스타일
            foreach (var s in series)
                ApplyLineTheme(s);

            model.InvalidatePlot(false);
        }

        private static void ApplyAxisTheme(Axis axis)
        {
            axis.AxislineColor = OxyColor.FromRgb(160, 160, 175);
            axis.AxislineThickness = 1;
            axis.TicklineColor = OxyColor.FromRgb(160, 160, 175);

            axis.MajorGridlineStyle = LineStyle.Solid;
            axis.MajorGridlineColor = OxyColor.FromRgb(235, 235, 242);
            axis.MajorGridlineThickness = 1;

            axis.MinorGridlineStyle = LineStyle.None;

            if (axis.AxisDistance < 6)
                axis.AxisDistance = 8;

            if (axis.MajorTickSize == 0) axis.MajorTickSize = 3;
            if (axis.MinorTickSize == 0) axis.MinorTickSize = 0;

            axis.TitleColor = OxyColor.FromRgb(60, 60, 80);
            if (axis.TextColor.IsUndefined())
                axis.TextColor = OxyColor.FromRgb(60, 60, 80);

            axis.FontSize = 11;
            axis.TitleFontSize = 12;
        }

        private static void ApplyLineTheme(LineSeries s)
        {
            if (s.StrokeThickness < 1.6)
                s.StrokeThickness = 1.8;

            s.MarkerType = MarkerType.None;

            if (s.MinimumSegmentLength <= 0)
                s.MinimumSegmentLength = 0.01;
        }
    }
}
