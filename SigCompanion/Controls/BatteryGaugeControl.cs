using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SigCompanion.Controls;

public sealed class BatteryGaugeControl : Control
{
    public static readonly StyledProperty<double> StateOfChargeProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, double>(nameof(StateOfCharge), 0d);

    public static readonly StyledProperty<bool> IsGridConnectedProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, bool>(nameof(IsGridConnected), true);

    static BatteryGaugeControl()
    {
        AffectsRender<BatteryGaugeControl>(StateOfChargeProperty, IsGridConnectedProperty);
    }

    public double StateOfCharge
    {
        get => GetValue(StateOfChargeProperty);
        set => SetValue(StateOfChargeProperty, value);
    }

    public bool IsGridConnected
    {
        get => GetValue(IsGridConnectedProperty);
        set => SetValue(IsGridConnectedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        var size = Math.Min(width, height);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(width / 2d, height / 2d);
        var radius = size * 0.38d;
        var ringThickness = Math.Max(8d, size * 0.12d);

        var startAngle = 99d;
        var totalSweep = 342d;
        const int segments = 10;
        var gapDegrees = 2.4d;

        var activeColor = IsGridConnected
            ? Color.Parse("#2BD640")
            : Color.Parse("#F9A825");

        var inactivePen = new Pen
        {
            Brush = new SolidColorBrush(Color.Parse("#7A7A7A")),
            Thickness = ringThickness,
            LineCap = PenLineCap.Flat,
        };

        var activePen = new Pen
        {
            Brush = new SolidColorBrush(activeColor),
            Thickness = ringThickness,
            LineCap = PenLineCap.Flat,
        };

        var dividerPen = new Pen
        {
            Brush = new SolidColorBrush(Color.Parse("#161616")),
            Thickness = Math.Max(2d, ringThickness * 0.12d),
            LineCap = PenLineCap.Round,
        };

        var clampedSoc = Math.Clamp(StateOfCharge, 0d, 100d);
        var segmentSweep = totalSweep / segments;

        for (var i = 0; i < segments; i++)
        {
            var segmentStart = startAngle + i * segmentSweep + gapDegrees / 2d;
            var segmentEnd = startAngle + (i + 1) * segmentSweep - gapDegrees / 2d;
            DrawArc(context, center, radius, segmentStart, segmentEnd, inactivePen);

            var segmentMinPercent = i * 10d;
            var coveredPercent = Math.Clamp(clampedSoc - segmentMinPercent, 0d, 10d);
            if (coveredPercent <= 0d)
            {
                continue;
            }

            var activeSegmentSweep = (segmentEnd - segmentStart) * (coveredPercent / 10d);
            DrawArc(context, center, radius, segmentStart, segmentStart + activeSegmentSweep, activePen);
        }

        for (var i = 0; i <= 10; i++)
        {
            var angleDegrees = startAngle + i * segmentSweep;
            var angle = DegreesToRadians(angleDegrees);

            var innerPoint = new Point(
                center.X + Math.Cos(angle) * (radius - ringThickness * 0.62d),
                center.Y + Math.Sin(angle) * (radius - ringThickness * 0.62d));

            var outerPoint = new Point(
                center.X + Math.Cos(angle) * (radius + ringThickness * 0.62d),
                center.Y + Math.Sin(angle) * (radius + ringThickness * 0.62d));

            context.DrawLine(dividerPen, innerPoint, outerPoint);
        }

        var innerCirclePen = new Pen
        {
            Brush = new SolidColorBrush(activeColor),
            Thickness = Math.Max(2d, size * 0.014d),
        };
        context.DrawEllipse(null, innerCirclePen, center, radius - ringThickness * 0.94d, radius - ringThickness * 0.94d);

        // center content (icons/text) is layered in XAML for now.
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double startAngle, double endAngle, Pen pen)
    {
        if (endAngle <= startAngle)
        {
            return;
        }

        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var isLargeArc = (endAngle - startAngle) > 180d;

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(start, false);
            stream.ArcTo(end, new Size(radius, radius), 0d, isLargeArc, SweepDirection.Clockwise);
            stream.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = DegreesToRadians(degrees);
        return new Point(
            center.X + Math.Cos(radians) * radius,
            center.Y + Math.Sin(radians) * radius);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
