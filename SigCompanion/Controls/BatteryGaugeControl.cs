using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SigCompanion.Controls;

public sealed class BatteryGaugeControl : Control
{
    private const double MinimumPower = 0.1;
    private const double AnimationDuration = 1d;
    private const double AnimationPause = 3d;
    private const double StartAngle = 99d;
    private const double TotalSweep = 342d;
    private const int Segments = 10;
    private const double SegmentSweep = TotalSweep / Segments;
    private const double GapDegrees = 2.4d;

    private readonly record struct RingKey(double Width, double Height, double SocPercent, bool IsGridConnected);

    private readonly record struct RingLayout(Point Center, double Radius, double RingThickness, double Size)
    {
        public static RingLayout Create(double width, double height)
        {
            var size = Math.Min(width, height);
            return new RingLayout(
                new Point(width / 2d, height / 2d),
                size * 0.38d,
                Math.Max(8d * 2d / 3d, size * 0.12d * 2d / 3d),
                size);
        }
    }

    public static readonly StyledProperty<double> StateOfChargeProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, double>(nameof(StateOfCharge), 0d);

    public static readonly StyledProperty<bool> IsGridConnectedProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, bool>(nameof(IsGridConnected), true);

    public static readonly StyledProperty<double> BatteryPowerProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, double>(nameof(BatteryPower), 0d);

    public static readonly StyledProperty<bool> EnableAnimationsProperty =
        AvaloniaProperty.Register<BatteryGaugeControl, bool>(nameof(EnableAnimations), true);

    private static readonly Color GreenColor = Color.Parse("#2BD640");
    private static readonly Color AmberColor = Color.Parse("#F9A825");
    private static readonly IBrush GreenBrush = new SolidColorBrush(GreenColor);
    private static readonly IBrush AmberBrush = new SolidColorBrush(AmberColor);
    private static readonly IBrush DarkGreenBrush = new SolidColorBrush(Darken(GreenColor, 0.6d));
    private static readonly IBrush DarkAmberBrush = new SolidColorBrush(Darken(AmberColor, 0.6d));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#7A7A7A"));
    private static readonly IBrush DividerBrush = new SolidColorBrush(Color.Parse("#161616"));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(67) };
    private readonly Pen _activePen = new() { LineCap = PenLineCap.Flat };
    private DrawingGroup _backgroundRing = new();
    private DrawingGroup _foregroundRing = new();
    private RingKey? _ringKey;
    private double _elapsed;

    static BatteryGaugeControl()
    {
        AffectsRender<BatteryGaugeControl>(StateOfChargeProperty, IsGridConnectedProperty, BatteryPowerProperty, EnableAnimationsProperty);
    }

    public BatteryGaugeControl()
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
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BatteryPowerProperty || change.Property == EnableAnimationsProperty)
        {
            UpdateTimer();
        }
    }

    private void UpdateTimer()
    {
        var isMoving = EnableAnimations && VisualRoot is not null && Math.Abs(BatteryPower) >= MinimumPower;
        if (isMoving)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public double BatteryPower
    {
        get => GetValue(BatteryPowerProperty);
        set => SetValue(BatteryPowerProperty, value);
    }

    public bool EnableAnimations
    {
        get => GetValue(EnableAnimationsProperty);
        set => SetValue(EnableAnimationsProperty, value);
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

        var clampedSoc = Math.Clamp(StateOfCharge, 0d, 100d);
        var key = new RingKey(width, height, clampedSoc, IsGridConnected);
        var layout = RingLayout.Create(width, height);
        if (_ringKey != key)
        {
            _ringKey = key;
            _backgroundRing = BuildBackgroundRing(layout, clampedSoc, IsGridConnected);
            _foregroundRing = BuildForegroundRing(layout, IsGridConnected);
        }

        _backgroundRing.Draw(context);

        var activePen = _activePen;
        activePen.Brush = IsGridConnected ? GreenBrush : AmberBrush;
        activePen.Thickness = layout.RingThickness;

        var fillSoc = clampedSoc;
        var cycle = AnimationDuration + AnimationPause;
        var phase = _elapsed % cycle;
        var isMoving = EnableAnimations && Math.Abs(BatteryPower) >= MinimumPower && phase < AnimationDuration;
        if (isMoving)
        {
            var progress = phase / AnimationDuration;
            fillSoc = BatteryPower < 0 ? clampedSoc * progress : clampedSoc * (1d - progress);
        }

        for (var i = 0; i < Segments; i++)
        {
            var segmentStart = StartAngle + i * SegmentSweep + GapDegrees / 2d;
            var segmentEnd = StartAngle + (i + 1) * SegmentSweep - GapDegrees / 2d;

            var fillPercent = Math.Clamp(fillSoc - i * 10d, 0d, 10d);
            if (fillPercent <= 0d)
            {
                continue;
            }

            var activeSegmentSweep = (segmentEnd - segmentStart) * (fillPercent / 10d);
            DrawArc(context, layout.Center, layout.Radius, segmentStart, segmentStart + activeSegmentSweep, activePen);
        }

        _foregroundRing.Draw(context);

        // center content (icons/text) is layered in XAML for now.
    }

    private static DrawingGroup BuildBackgroundRing(RingLayout layout, double clampedSoc, bool isGridConnected)
    {
        var group = new DrawingGroup();
        var darkPen = new Pen
        {
            Brush = isGridConnected ? DarkGreenBrush : DarkAmberBrush,
            Thickness = layout.RingThickness,
            LineCap = PenLineCap.Flat,
        };
        var inactivePen = new Pen
        {
            Brush = InactiveBrush,
            Thickness = layout.RingThickness,
            LineCap = PenLineCap.Flat,
        };

        for (var i = 0; i < Segments; i++)
        {
            var segmentStart = StartAngle + i * SegmentSweep + GapDegrees / 2d;
            var segmentEnd = StartAngle + (i + 1) * SegmentSweep - GapDegrees / 2d;
            AddArc(group, layout.Center, layout.Radius, segmentStart, segmentEnd, inactivePen);

            var coveredPercent = Math.Clamp(clampedSoc - i * 10d, 0d, 10d);
            if (coveredPercent > 0d)
            {
                var darkSweep = (segmentEnd - segmentStart) * (coveredPercent / 10d);
                AddArc(group, layout.Center, layout.Radius, segmentStart, segmentStart + darkSweep, darkPen);
            }
        }

        return group;
    }

    private static DrawingGroup BuildForegroundRing(RingLayout layout, bool isGridConnected)
    {
        var group = new DrawingGroup();
        var dividerPen = new Pen
        {
            Brush = DividerBrush,
            Thickness = Math.Max(2d, layout.RingThickness * 0.12d),
            LineCap = PenLineCap.Round,
        };
        var innerCirclePen = new Pen
        {
            Brush = isGridConnected ? GreenBrush : AmberBrush,
            Thickness = Math.Max(2d, layout.Size * 0.014d),
        };

        var dividers = new StreamGeometry();
        using (var stream = dividers.Open())
        {
            for (var i = 0; i <= Segments; i++)
            {
                var angle = DegreesToRadians(StartAngle + i * SegmentSweep);
                var innerPoint = new Point(
                    layout.Center.X + Math.Cos(angle) * (layout.Radius - layout.RingThickness * 0.62d),
                    layout.Center.Y + Math.Sin(angle) * (layout.Radius - layout.RingThickness * 0.62d));
                var outerPoint = new Point(
                    layout.Center.X + Math.Cos(angle) * (layout.Radius + layout.RingThickness * 0.62d),
                    layout.Center.Y + Math.Sin(angle) * (layout.Radius + layout.RingThickness * 0.62d));

                stream.BeginFigure(innerPoint, false);
                stream.LineTo(outerPoint);
                stream.EndFigure(false);
            }
        }

        group.Children.Add(new GeometryDrawing { Geometry = dividers, Pen = dividerPen });

        var innerRadius = layout.Radius - layout.RingThickness * 0.94d;
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry { Rect = new Rect(layout.Center.X - innerRadius, layout.Center.Y - innerRadius, innerRadius * 2d, innerRadius * 2d) },
            Pen = innerCirclePen,
        });

        return group;
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double startAngle, double endAngle, Pen pen)
    {
        var geometry = CreateArcGeometry(center, radius, startAngle, endAngle);
        if (geometry is not null)
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    private static void AddArc(DrawingGroup group, Point center, double radius, double startAngle, double endAngle, Pen pen)
    {
        var geometry = CreateArcGeometry(center, radius, startAngle, endAngle);
        if (geometry is not null)
        {
            group.Children.Add(new GeometryDrawing { Geometry = geometry, Pen = pen });
        }
    }

    private static StreamGeometry? CreateArcGeometry(Point center, double radius, double startAngle, double endAngle)
    {
        if (endAngle <= startAngle)
        {
            return null;
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

        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = DegreesToRadians(degrees);
        return new Point(
            center.X + Math.Cos(radians) * radius,
            center.Y + Math.Sin(radians) * radius);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static Color Darken(Color color, double factor) =>
        new(color.A, (byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));

    private static Color Lerp(Color from, Color to, double t) =>
        new(255, (byte)(from.R + (to.R - from.R) * t), (byte)(from.G + (to.G - from.G) * t), (byte)(from.B + (to.B - from.B) * t));
}
