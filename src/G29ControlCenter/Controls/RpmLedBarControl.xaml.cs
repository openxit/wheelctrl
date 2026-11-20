using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace G29ControlCenter.Controls;

public partial class RpmLedBarControl : UserControl
{
    private static readonly SolidColorBrush GreenLedBrush = FreezeBrush("#1B8A4B");
    private static readonly SolidColorBrush AmberLedBrush = FreezeBrush("#D97706");
    private static readonly SolidColorBrush RedLedBrush = FreezeBrush("#C42B1C");

    public static readonly DependencyProperty NormalizedRpmProperty =
        DependencyProperty.Register(
            nameof(NormalizedRpm),
            typeof(double),
            typeof(RpmLedBarControl),
            new PropertyMetadata(0.0, OnNormalizedRpmChanged));

    public RpmLedBarControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateLeds();
    }

    public double NormalizedRpm
    {
        get => (double)GetValue(NormalizedRpmProperty);
        set => SetValue(NormalizedRpmProperty, value);
    }

    private static void OnNormalizedRpmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RpmLedBarControl control)
        {
            control.UpdateLeds();
        }
    }

    private void UpdateLeds()
    {
        Brush offBrush = TryFindResource("LedOffBrush") as Brush ?? Brushes.LightGray;
        double rpm = Math.Clamp(NormalizedRpm, 0.0, 1.0);

        // Exakt synchron zu LogiPlayLeds(index, rpm, 0.15f, 1.12f): Rot erst ab 90 %
        int litPairs = rpm >= 0.90 ? 5
            : rpm >= 0.75 ? 4
            : rpm >= 0.55 ? 3
            : rpm >= 0.35 ? 2
            : rpm >= 0.15 ? 1
            : 0;

        SetPair(L1, R1, litPairs >= 1 ? GreenLedBrush : offBrush);
        SetPair(L2, R2, litPairs >= 2 ? GreenLedBrush : offBrush);
        SetPair(L3, R3, litPairs >= 3 ? AmberLedBrush : offBrush);
        SetPair(L4, R4, litPairs >= 4 ? AmberLedBrush : offBrush);
        SetPair(L5, R5, litPairs >= 5 ? RedLedBrush : offBrush);
    }

    private static void SetPair(Border left, Border right, Brush brush)
    {
        left.Background = brush;
        right.Background = brush;
    }

    private static SolidColorBrush FreezeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
