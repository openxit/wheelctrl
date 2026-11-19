using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace G29ControlCenter.Controls;

public partial class PedalBarControl : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(PedalBarControl), new PropertyMetadata("Pedal"));

    public static readonly DependencyProperty PercentageProperty =
        DependencyProperty.Register(
            nameof(Percentage),
            typeof(double),
            typeof(PedalBarControl),
            new PropertyMetadata(0.0, OnPercentageChanged));

    public static readonly DependencyProperty RawValueProperty =
        DependencyProperty.Register(nameof(RawValue), typeof(int), typeof(PedalBarControl), new PropertyMetadata(32767));

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(PedalBarControl),
            new PropertyMetadata(Brushes.SteelBlue));

    public PedalBarControl()
    {
        InitializeComponent();
        SetResourceReference(BarBrushProperty, SystemColors.AccentColorBrushKey);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public int RawValue
    {
        get => (int)GetValue(RawValueProperty);
        set => SetValue(RawValueProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PedalBarControl control)
        {
            control.UpdateFillHeight();
        }
    }

    private void OnBarTrackSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFillHeight();
    }

    private void UpdateFillHeight()
    {
        if (FillRect is null || BarTrackGrid is null)
        {
            return;
        }

        double trackHeight = BarTrackGrid.ActualHeight;
        if (trackHeight <= 0)
        {
            return;
        }

        double ratio = Math.Clamp(Percentage / 100.0, 0.0, 1.0);
        FillRect.Height = trackHeight * ratio;
    }
}
