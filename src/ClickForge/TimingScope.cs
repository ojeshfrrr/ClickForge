using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;

// A bounded, event-driven drawing; no animation or rendering-event subscription.
internal sealed class TimingScope : FrameworkElement
{
    private LogicalClickEvent[] samples = [];
    internal int SampleCount => samples.Length;
    internal void SetSamples(LogicalClickEvent[] value)
    {
        var recent = value.TakeLast(100).ToArray();
        if (samples.SequenceEqual(recent)) return;
        samples = recent; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var line = (Brush)FindResource("Line");
        dc.DrawRoundedRectangle((Brush)FindResource("Base"), new Pen(line, 1), new Rect(RenderSize), 5, 5);
        double width = Math.Max(1, ActualWidth - 65), height = Math.Max(1, ActualHeight - 35);
        double maximum = Math.Max(1, samples.Length == 0 ? 200 : samples.Max(s => Math.Max(s.RequestedIntervalMs, s.MeasuredIntervalMs)) * 1.1);
        for (int i = 0; i <= 2; i++)
        {
            double y = 10 + height * i / 2;
            dc.DrawLine(new Pen(line, 1), new(52, y), new(52 + width, y));
            var label = new FormattedText($"{maximum * (2 - i) / 2:0} ms", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 10, (Brush)FindResource("Muted"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new(4, y));
        }
        void Plot(Func<LogicalClickEvent, double> interval, string brush)
        {
            if (samples.Length == 0) return;
            var geometry = new StreamGeometry();
            using (var draw = geometry.Open())
            {
                for (int n = 0; n < samples.Length; n++)
                {
                    var point = new Point(52 + width * n / Math.Max(1, samples.Length - 1), 10 + height * (1 - interval(samples[n]) / maximum));
                    if (n == 0) draw.BeginFigure(point, false, false); else draw.LineTo(point, true, false);
                }
            }
            geometry.Freeze(); dc.DrawGeometry(null, new Pen((Brush)FindResource(brush), 1.8), geometry);
            if (samples.Length == 1)
                dc.DrawEllipse((Brush)FindResource(brush), null, new(52, 10 + height * (1 - interval(samples[0]) / maximum)), 2.5, 2.5);
        }
        Plot(s => s.RequestedIntervalMs, "Accent"); Plot(s => s.MeasuredIntervalMs, "Success");
    }
}
