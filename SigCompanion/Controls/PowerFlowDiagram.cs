using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Controls;

public sealed class PowerFlowDiagram : Control
{
    private const double MinimumFlow = 0.1;
    private const double StrokeThickness = 5;
    private const double ArrowLength = 5;
    private const double ArrowHalfWidth = 5;
    private const double NodeInsetDegrees = 14;
    private const double NodeInsetFraction = 0.12;
    private const double LabelOffset = 14;
    private const double LabelFontSize = 13;
    private const int ArcSegments = 32;
    private const double FlowSpeed = 40;

    private static readonly Color SolarColor = Color.Parse("#E0B800");
    private static readonly Color GridColor = Color.Parse("#CCCCCC");
    private static readonly Color HomeColor = Color.Parse("#2AA7E0");
    private static readonly Color BatteryColor = Color.Parse("#3CC83C");

    public static readonly StyledProperty<PowerFlowAllocation?> FlowsProperty =
        AvaloniaProperty.Register<PowerFlowDiagram, PowerFlowAllocation?>(nameof(Flows));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private double _elapsed;

    static PowerFlowDiagram()
    {
        AffectsRender<PowerFlowDiagram>(FlowsProperty);
    }

    public PowerFlowDiagram()
    {
        _timer.Tick += (_, _) =>
        {
            _elapsed += _timer.Interval.TotalSeconds;
            InvalidateVisual();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public PowerFlowAllocation? Flows
    {
        get => GetValue(FlowsProperty);
        set => SetValue(FlowsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Flows is not { } flows)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        var center = new Point(width / 2d, height / 2d);
        var radius = 0.42d * Math.Min(width, height);

        var solarPosition = PointOnCircle(center, radius, 225d);
        var gridPosition = PointOnCircle(center, radius, 315d);
        var homePosition = PointOnCircle(center, radius, 45d);
        var batteryPosition = PointOnCircle(center, radius, 135d);

        DrawArc(context, center, radius, 225d, 315d, flows.SolarToGrid, SolarColor, GridColor);
        DrawArc(context, center, radius, 315d, 405d, flows.GridToHome, GridColor, HomeColor);
        DrawArc(context, center, radius, 135d, 45d, flows.BatteryToHome, BatteryColor, HomeColor);
        DrawArc(context, center, radius, 225d, 135d, flows.SolarToBattery, SolarColor, BatteryColor);

        DrawLine(context, solarPosition, homePosition, flows.SolarToHome, SolarColor, HomeColor);
        DrawLine(context, gridPosition, batteryPosition, flows.GridToBattery, GridColor, BatteryColor);
        DrawLine(context, batteryPosition, gridPosition, flows.BatteryToGrid, BatteryColor, GridColor);
    }

    private void DrawArc(DrawingContext context, Point center, double radius, double fromDegrees, double toDegrees, double value, Color fromColor, Color toColor)
    {
        if (value < MinimumFlow)
        {
            return;
        }

        var sign = Math.Sign(toDegrees - fromDegrees);
        var start = fromDegrees + sign * NodeInsetDegrees;
        var end = toDegrees - sign * NodeInsetDegrees;

        var points = new Point[ArcSegments + 1];
        for (var i = 0; i <= ArcSegments; i++)
        {
            points[i] = PointOnCircle(center, radius, start + (end - start) * i / ArcSegments);
        }

        var middle = PointOnCircle(center, radius, (start + end) / 2d);
        var outward = Normalize(middle - center);
        var labelPosition = middle + outward * LabelOffset;

        DrawFlow(context, points, value, fromColor, toColor, labelPosition);
    }

    private void DrawLine(DrawingContext context, Point from, Point to, double value, Color fromColor, Color toColor)
    {
        if (value < MinimumFlow)
        {
            return;
        }

        var points = new[]
        {
            from + (to - from) * NodeInsetFraction,
            to - (to - from) * NodeInsetFraction
        };

        var middle = new Point((from.X + to.X) / 2d, (from.Y + to.Y) / 2d);
        var delta = to - from;
        var perpendicular = Normalize(new Vector(-delta.Y, delta.X));
        var labelPosition = middle + perpendicular * LabelOffset;

        DrawFlow(context, points, value, fromColor, toColor, labelPosition);
    }

    private void DrawFlow(DrawingContext context, Point[] points, double value, Color fromColor, Color toColor, Point labelPosition)
    {
        var first = points[0];
        var last = points[^1];

        var lengths = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            lengths[i] = lengths[i - 1] + new Vector(points[i].X - points[i - 1].X, points[i].Y - points[i - 1].Y).Length;
        }

        var totalLength = lengths[^1];
        if (totalLength <= 0)
        {
            return;
        }

        var offset = _elapsed * FlowSpeed;
        Color ColorAt(double distance) => Lerp(fromColor, toColor, Triangle(Frac((distance - offset) / totalLength)));

        const int subdivisions = 8;
        var dense = new Point[(points.Length - 1) * subdivisions + 1];
        for (var i = 1; i < points.Length; i++)
        {
            for (var j = 0; j < subdivisions; j++)
            {
                var f = (double)j / subdivisions;
                var a = points[i - 1];
                var b = points[i];
                dense[(i - 1) * subdivisions + j] = new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
            }
        }
        dense[^1] = last;

        var distances = new double[dense.Length];
        for (var i = 1; i < dense.Length; i++)
        {
            var dx = dense[i].X - dense[i - 1].X;
            var dy = dense[i].Y - dense[i - 1].Y;
            distances[i] = distances[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }

        for (var i = 1; i < dense.Length; i++)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(dense[i - 1], RelativeUnit.Absolute),
                EndPoint = new RelativePoint(dense[i], RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(ColorAt(distances[i - 1]), 0),
                    new GradientStop(ColorAt(distances[i]), 1)
                }
            };
            var pen = new Pen(brush, StrokeThickness) { LineCap = PenLineCap.Round };
            context.DrawLine(pen, dense[i - 1], dense[i]);
        }

        var direction = Normalize(last - points[^2]);
        var perpendicular = new Vector(-direction.Y, direction.X);
        var tip = last + direction * ArrowLength;
        var baseCentre = last - direction * ArrowLength;
        var left = baseCentre + perpendicular * ArrowHalfWidth;
        var right = baseCentre - perpendicular * ArrowHalfWidth;

        var arrowGeometry = new StreamGeometry();
        using (var arrowContext = arrowGeometry.Open())
        {
            arrowContext.BeginFigure(tip, true);
            arrowContext.LineTo(left);
            arrowContext.LineTo(right);
            arrowContext.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(toColor), null, arrowGeometry);

        var text = new FormattedText(
            value.ToString("0.0", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            LabelFontSize,
            new SolidColorBrush(fromColor));

        context.DrawText(text, labelPosition - new Vector(text.Width / 2d, text.Height / 2d));
    }

    private static double Frac(double value) => value - Math.Floor(value);

    private static double Triangle(double t) => 1d - Math.Abs(2d * t - 1d);

    private static Color Lerp(Color from, Color to, double t) =>
        new(255, (byte)(from.R + (to.R - from.R) * t), (byte)(from.G + (to.G - from.G) * t), (byte)(from.B + (to.B - from.B) * t));

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static Vector Normalize(Vector vector)
    {
        var length = vector.Length;
        return length == 0 ? vector : new Vector(vector.X / length, vector.Y / length);
    }
}
