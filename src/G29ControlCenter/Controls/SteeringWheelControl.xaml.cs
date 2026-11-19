using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace G29ControlCenter.Controls;

public partial class SteeringWheelControl : UserControl
{
    public static readonly DependencyProperty CurrentAngleProperty =
        DependencyProperty.Register(
            nameof(CurrentAngle),
            typeof(double),
            typeof(SteeringWheelControl),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty TargetAngleProperty =
        DependencyProperty.Register(
            nameof(TargetAngle),
            typeof(double),
            typeof(SteeringWheelControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty OperatingRangeProperty =
        DependencyProperty.Register(
            nameof(OperatingRange),
            typeof(int),
            typeof(SteeringWheelControl),
            new PropertyMetadata(900, OnOperatingRangeChanged));

    public static readonly DependencyProperty ShowTargetMarkerProperty =
        DependencyProperty.Register(
            nameof(ShowTargetMarker),
            typeof(bool),
            typeof(SteeringWheelControl),
            new PropertyMetadata(false, OnShowTargetMarkerChanged));

    public static readonly DependencyProperty ShowDegreeScaleProperty =
        DependencyProperty.Register(
            nameof(ShowDegreeScale),
            typeof(bool),
            typeof(SteeringWheelControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty LeftPaddlePressedProperty =
        DependencyProperty.Register(
            nameof(LeftPaddlePressed),
            typeof(bool),
            typeof(SteeringWheelControl),
            new PropertyMetadata(false, OnPaddleStateChanged));

    public static readonly DependencyProperty RightPaddlePressedProperty =
        DependencyProperty.Register(
            nameof(RightPaddlePressed),
            typeof(bool),
            typeof(SteeringWheelControl),
            new PropertyMetadata(false, OnPaddleStateChanged));

    private bool _isDragging;
    private double _dragPreviousPointerAngle;
    private double _dragAccumulatedAngle;

    public event EventHandler<double>? WheelDraggedAngle;

    public SteeringWheelControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Cursor = ShowTargetMarker ? Cursors.Hand : Cursors.Arrow;
            RebuildScaleTicks();
        };
    }

    public double CurrentAngle
    {
        get => (double)GetValue(CurrentAngleProperty);
        set => SetValue(CurrentAngleProperty, value);
    }

    public double TargetAngle
    {
        get => (double)GetValue(TargetAngleProperty);
        set => SetValue(TargetAngleProperty, value);
    }

    public int OperatingRange
    {
        get => (int)GetValue(OperatingRangeProperty);
        set => SetValue(OperatingRangeProperty, value);
    }

    public bool ShowTargetMarker
    {
        get => (bool)GetValue(ShowTargetMarkerProperty);
        set => SetValue(ShowTargetMarkerProperty, value);
    }

    public bool ShowDegreeScale
    {
        get => (bool)GetValue(ShowDegreeScaleProperty);
        set => SetValue(ShowDegreeScaleProperty, value);
    }

    public bool LeftPaddlePressed
    {
        get => (bool)GetValue(LeftPaddlePressedProperty);
        set => SetValue(LeftPaddlePressedProperty, value);
    }

    public bool RightPaddlePressed
    {
        get => (bool)GetValue(RightPaddlePressedProperty);
        set => SetValue(RightPaddlePressedProperty, value);
    }

    private static void OnShowTargetMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SteeringWheelControl control)
        {
            control.Cursor = control.ShowTargetMarker ? Cursors.Hand : Cursors.Arrow;
        }
    }

    private static void OnOperatingRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SteeringWheelControl control)
        {
            control.RebuildScaleTicks();
        }
    }

    private static void OnPaddleStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SteeringWheelControl control)
        {
            control.UpdatePaddleVisuals();
        }
    }

    private void UpdatePaddleVisuals()
    {
        Brush? activeBrush = TryFindResource(SystemColors.AccentColorBrushKey) as Brush;
        Brush? idleBrush = TryFindResource("SurfaceSubtleBrush") as Brush;

        LeftPaddlePath.Fill = LeftPaddlePressed ? (activeBrush ?? Brushes.DodgerBlue) : (idleBrush ?? Brushes.LightGray);
        RightPaddlePath.Fill = RightPaddlePressed ? (activeBrush ?? Brushes.DodgerBlue) : (idleBrush ?? Brushes.LightGray);
    }

    private void RebuildScaleTicks()
    {
        if (ScaleTicksLayer is null)
        {
            return;
        }

        ScaleTicksLayer.Children.Clear();

        const double cx = 170.0;
        const double cy = 170.0;
        const double outerR = 148.0;

        Brush? tickBrush = TryFindResource("SecondaryTextBrush") as Brush ?? Brushes.Gray;
        Brush? trackBrush = TryFindResource("WheelTrackBrush") as Brush ?? Brushes.LightGray;

        var ring = new Ellipse
        {
            Width = outerR * 2,
            Height = outerR * 2,
            Stroke = trackBrush,
            StrokeThickness = 1.0
        };
        Canvas.SetLeft(ring, cx - outerR);
        Canvas.SetTop(ring, cy - outerR);
        ScaleTicksLayer.Children.Add(ring);

        // 24 gleichmäßige Skalenstriche rund um den Kranz (alle 15° visuell)
        for (int deg = 0; deg < 360; deg += 15)
        {
            bool isMajor = deg % 45 == 0;
            double len = isMajor ? 8.0 : 4.0;
            double rad = (deg - 90.0) * Math.PI / 180.0;

            double x1 = cx + (outerR * Math.Cos(rad));
            double y1 = cy + (outerR * Math.Sin(rad));
            double x2 = cx + ((outerR + len) * Math.Cos(rad));
            double y2 = cy + ((outerR + len) * Math.Sin(rad));

            var tick = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = tickBrush,
                StrokeThickness = isMajor ? 1.5 : 1.0
            };
            ScaleTicksLayer.Children.Add(tick);
        }

        double halfRange = Math.Max(90.0, OperatingRange / 2.0);
        AddScaleLabel($"-{halfRange:0}°", 14, 160, tickBrush);
        AddScaleLabel("0°", 163, 0, tickBrush);
        AddScaleLabel($"+{halfRange:0}°", 294, 160, tickBrush);
    }

    private void AddScaleLabel(string text, double left, double top, Brush foreground)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            Foreground = foreground
        };
        System.Windows.Documents.Typography.SetNumeralAlignment(label, FontNumeralAlignment.Tabular);
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        ScaleTicksLayer.Children.Add(label);
    }

    private void OnWheelMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShowTargetMarker)
        {
            return;
        }

        Point pt = e.GetPosition(DialCanvas);
        double dx = pt.X - 170.0;
        double dy = pt.Y - 170.0;
        if ((dx * dx + dy * dy) < (24.0 * 24.0))
        {
            return;
        }

        _isDragging = true;
        _dragPreviousPointerAngle = ComputePointerDegrees(pt);
        _dragAccumulatedAngle = ShowTargetMarker ? TargetAngle : CurrentAngle;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnWheelMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        Point pt = e.GetPosition(DialCanvas);
        double currentPointer = ComputePointerDegrees(pt);
        double delta = currentPointer - _dragPreviousPointerAngle;

        // Kürzeste Winkeldifferenz über ±180°-Übergang für kontinuierliche Mehrfach-Umdrehungen
        delta = ((delta + 540.0) % 360.0) - 180.0;
        _dragPreviousPointerAngle = currentPointer;

        double halfRange = OperatingRange / 2.0;
        _dragAccumulatedAngle = Math.Clamp(_dragAccumulatedAngle + delta, -halfRange, halfRange);
        double rounded = Math.Round(_dragAccumulatedAngle, 1);

        TargetAngle = rounded;
        WheelDraggedAngle?.Invoke(this, rounded);
    }

    private void OnWheelMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnWheelLostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private static double ComputePointerDegrees(Point pt)
    {
        double dx = pt.X - 170.0;
        double dy = pt.Y - 170.0;
        return Math.Atan2(dx, -dy) * (180.0 / Math.PI);
    }
}
