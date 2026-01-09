using System;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace BliMonitorTest.controls
{
    public static class PlotModelCloneHelper
    {
        public static PlotModel CloneForView(PlotModel src)
        {
            if (src == null) return null;

            var dst = new PlotModel
            {
                Title = src.Title,
                Subtitle = src.Subtitle,
                PlotAreaBorderColor = src.PlotAreaBorderColor,
                TextColor = src.TextColor,
                TitleColor = src.TitleColor,
                Background = src.Background,
                DefaultFont = src.DefaultFont,
                DefaultFontSize = src.DefaultFontSize,
            };

            // Axes 복사
            foreach (var ax in src.Axes)
            {
                dst.Axes.Add(CloneAxis(ax));
            }

            // Series 복사 (자주 쓰는 타입만 우선 지원)
            foreach (var s in src.Series)
            {
                dst.Series.Add(CloneSeries(s));
            }

            return dst;
        }

        private static Axis CloneAxis(Axis ax)
        {
            Axis copy;

            if (ax is DateTimeAxis)
                copy = new DateTimeAxis();
            else if (ax is LogarithmicAxis)
                copy = new LogarithmicAxis();
            else if (ax is CategoryAxis)
                copy = new CategoryAxis();
            else
                copy = new LinearAxis();

            copy.Position = ax.Position;
            copy.Key = ax.Key;
            copy.Title = ax.Title;

            copy.Minimum = ax.Minimum;
            copy.Maximum = ax.Maximum;

            copy.MajorGridlineStyle = ax.MajorGridlineStyle;
            copy.MinorGridlineStyle = ax.MinorGridlineStyle;
            copy.MajorGridlineColor = ax.MajorGridlineColor;
            copy.MinorGridlineColor = ax.MinorGridlineColor;

            copy.TextColor = ax.TextColor;
            copy.TicklineColor = ax.TicklineColor;
            copy.AxislineColor = ax.AxislineColor;

            copy.IsPanEnabled = ax.IsPanEnabled;
            copy.IsZoomEnabled = ax.IsZoomEnabled;

            return copy;
        }

        private static Series CloneSeries(Series s)
        {
            if (s is LineSeries)
            {
                var ls = (LineSeries)s;

                var copy = new LineSeries
                {
                    Title = ls.Title,
                    Color = ls.Color,
                    StrokeThickness = ls.StrokeThickness,
                    LineStyle = ls.LineStyle,
                    MarkerType = ls.MarkerType,
                    MarkerSize = ls.MarkerSize,
                    MarkerFill = ls.MarkerFill,
                    MarkerStroke = ls.MarkerStroke,
                    MarkerStrokeThickness = ls.MarkerStrokeThickness,
                    XAxisKey = ls.XAxisKey,
                    YAxisKey = ls.YAxisKey,
                };

                // ✅ 포인트 딥카피(데이터 간섭 방지)
                foreach (var p in ls.Points)
                    copy.Points.Add(new DataPoint(p.X, p.Y));

                return copy;
            }

            if (s is ScatterSeries)
            {
                var ss = (ScatterSeries)s;

                var copy = new ScatterSeries
                {
                    Title = ss.Title,
                    MarkerType = ss.MarkerType,
                    MarkerSize = ss.MarkerSize,
                    MarkerFill = ss.MarkerFill,
                    MarkerStroke = ss.MarkerStroke,
                    MarkerStrokeThickness = ss.MarkerStrokeThickness,
                    XAxisKey = ss.XAxisKey,
                    YAxisKey = ss.YAxisKey,
                };

                foreach (var p in ss.Points)
                    copy.Points.Add(new ScatterPoint(p.X, p.Y, p.Size, p.Value, p.Tag));

                return copy;
            }

            // 다른 시리즈 타입이면 여기 케이스 추가 필요
            if (s is ICloneable)
            {
                var cloned = ((ICloneable)s).Clone() as Series;
                if (cloned != null) return cloned;
            }

            throw new NotSupportedException("지원하지 않는 Series 타입: " + s.GetType().Name);
        }
    }
}
