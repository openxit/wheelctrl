using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using G29ControlCenter.Models;

namespace G29ControlCenter.Controls;

public partial class VisualGamepadControl : UserControl
{
    private static readonly Brush DefaultBg = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x30));
    private static readonly Brush DefaultBorder = new SolidColorBrush(Color.FromRgb(0x48, 0x4E, 0x5C));
    private static readonly Brush SelectedBorder = new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00));

    public static readonly DependencyProperty LeftStickXNormProperty =
        DependencyProperty.Register(nameof(LeftStickXNorm), typeof(double), typeof(VisualGamepadControl),
            new PropertyMetadata(0.0, OnVisualStateChanged));

    public static readonly DependencyProperty LeftTriggerPercentProperty =
        DependencyProperty.Register(nameof(LeftTriggerPercent), typeof(double), typeof(VisualGamepadControl),
            new PropertyMetadata(0.0, OnVisualStateChanged));

    public static readonly DependencyProperty RightTriggerPercentProperty =
        DependencyProperty.Register(nameof(RightTriggerPercent), typeof(double), typeof(VisualGamepadControl),
            new PropertyMetadata(0.0, OnVisualStateChanged));

    public static readonly DependencyProperty PressedMaskProperty =
        DependencyProperty.Register(nameof(PressedMask), typeof(VirtualXboxButtonMask), typeof(VisualGamepadControl),
            new PropertyMetadata(VirtualXboxButtonMask.None, OnVisualStateChanged));

    public static readonly DependencyProperty IsDs4ModeProperty =
        DependencyProperty.Register(nameof(IsDs4Mode), typeof(bool), typeof(VisualGamepadControl),
            new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty SelectedTargetProperty =
        DependencyProperty.Register(nameof(SelectedTarget), typeof(VirtualButtonTarget), typeof(VisualGamepadControl),
            new FrameworkPropertyMetadata(VirtualButtonTarget.A, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualStateChanged));

    public VisualGamepadControl()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    public double LeftStickXNorm
    {
        get => (double)GetValue(LeftStickXNormProperty);
        set => SetValue(LeftStickXNormProperty, value);
    }

    public double LeftTriggerPercent
    {
        get => (double)GetValue(LeftTriggerPercentProperty);
        set => SetValue(LeftTriggerPercentProperty, value);
    }

    public double RightTriggerPercent
    {
        get => (double)GetValue(RightTriggerPercentProperty);
        set => SetValue(RightTriggerPercentProperty, value);
    }

    public VirtualXboxButtonMask PressedMask
    {
        get => (VirtualXboxButtonMask)GetValue(PressedMaskProperty);
        set => SetValue(PressedMaskProperty, value);
    }

    public bool IsDs4Mode
    {
        get => (bool)GetValue(IsDs4ModeProperty);
        set => SetValue(IsDs4ModeProperty, value);
    }

    public VirtualButtonTarget SelectedTarget
    {
        get => (VirtualButtonTarget)GetValue(SelectedTargetProperty);
        set => SetValue(SelectedTargetProperty, value);
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VisualGamepadControl control)
        {
            control.UpdateVisuals();
        }
    }

    private void OnPadButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagName && Enum.TryParse(tagName, out VirtualButtonTarget target))
        {
            SelectedTarget = target;
        }
    }

    private void UpdateVisuals()
    {
        LeftStickTransform.X = Math.Clamp(LeftStickXNorm / 100.0, -1.0, 1.0) * 14.0;
        LeftTriggerFill.Width = Math.Clamp(LeftTriggerPercent / 100.0, 0.0, 1.0) * 118.0;
        RightTriggerFill.Width = Math.Clamp(RightTriggerPercent / 100.0, 0.0, 1.0) * 118.0;

        bool ds4 = IsDs4Mode;
        LeftTriggerText.Text = ds4 ? "L2" : "LT";
        RightTriggerText.Text = ds4 ? "R2" : "RT";
        BtnLB.Content = ds4 ? "L1" : "LB";
        BtnRB.Content = ds4 ? "R1" : "RB";
        BtnA.Content = ds4 ? "✕" : "A";
        BtnB.Content = ds4 ? "○" : "B";
        BtnX.Content = ds4 ? "□" : "X";
        BtnY.Content = ds4 ? "△" : "Y";
        BtnBack.Content = ds4 ? "Share" : "Back";
        BtnStart.Content = ds4 ? "Opt" : "Start";
        BtnLS.Content = ds4 ? "L3" : "LS";
        BtnRS.Content = ds4 ? "R3" : "RS";

        Brush accent = TryFindResource(SystemColors.AccentColorBrushKey) as Brush ?? Brushes.DodgerBlue;
        VirtualXboxButtonMask mask = PressedMask;

        UpdateButton(BtnA, VirtualButtonTarget.A, (mask & VirtualXboxButtonMask.A) != 0, accent);
        UpdateButton(BtnB, VirtualButtonTarget.B, (mask & VirtualXboxButtonMask.B) != 0, accent);
        UpdateButton(BtnX, VirtualButtonTarget.X, (mask & VirtualXboxButtonMask.X) != 0, accent);
        UpdateButton(BtnY, VirtualButtonTarget.Y, (mask & VirtualXboxButtonMask.Y) != 0, accent);
        UpdateButton(BtnLB, VirtualButtonTarget.LeftShoulder, (mask & VirtualXboxButtonMask.LeftShoulder) != 0, accent);
        UpdateButton(BtnRB, VirtualButtonTarget.RightShoulder, (mask & VirtualXboxButtonMask.RightShoulder) != 0, accent);
        UpdateButton(BtnBack, VirtualButtonTarget.Back, (mask & VirtualXboxButtonMask.Back) != 0, accent);
        UpdateButton(BtnStart, VirtualButtonTarget.Start, (mask & VirtualXboxButtonMask.Start) != 0, accent);
        UpdateButton(BtnLS, VirtualButtonTarget.LeftThumb, (mask & VirtualXboxButtonMask.LeftThumb) != 0, accent);
        UpdateButton(BtnRS, VirtualButtonTarget.RightThumb, (mask & VirtualXboxButtonMask.RightThumb) != 0, accent);
        UpdateButton(BtnUp, VirtualButtonTarget.DPadUp, (mask & VirtualXboxButtonMask.Up) != 0, accent);
        UpdateButton(BtnDown, VirtualButtonTarget.DPadDown, (mask & VirtualXboxButtonMask.Down) != 0, accent);
        UpdateButton(BtnLeft, VirtualButtonTarget.DPadLeft, (mask & VirtualXboxButtonMask.Left) != 0, accent);
        UpdateButton(BtnRight, VirtualButtonTarget.DPadRight, (mask & VirtualXboxButtonMask.Right) != 0, accent);
    }

    private void UpdateButton(Button btn, VirtualButtonTarget target, bool isPressed, Brush accent)
    {
        btn.Background = isPressed ? accent : DefaultBg;
        btn.BorderBrush = SelectedTarget == target ? SelectedBorder : DefaultBorder;
    }
}
