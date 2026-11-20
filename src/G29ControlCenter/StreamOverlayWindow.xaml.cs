using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using G29ControlCenter.Models;
using G29ControlCenter.ViewModels;

namespace G29ControlCenter;

public partial class StreamOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int MinScalePercent = 40;
    private const int MaxScalePercent = 250;

    private readonly OverlaySettings _settings;
    private bool _isResizeDragging;
    private Point _resizeStartScreenPoint;
    private int _resizeStartScalePercent;

    public StreamOverlayWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = viewModel.Overlay;
        _settings.PropertyChanged += OnOverlaySettingsPropertyChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyClickThroughStyle();
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.PropertyChanged -= OnOverlaySettingsPropertyChanged;
        base.OnClosed(e);
    }

    private void OnOverlaySettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlaySettings.ClickThrough))
        {
            ApplyClickThroughStyle();
        }
    }

    private void ApplyClickThroughStyle()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        if (_settings.ClickThrough)
        {
            SetWindowLong(hwnd, GwlExStyle, exStyle | WsExTransparent);
        }
        else
        {
            SetWindowLong(hwnd, GwlExStyle, exStyle & ~WsExTransparent);
        }
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.ClickThrough || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _isResizeDragging = true;
            _resizeStartScreenPoint = PointToScreen(e.GetPosition(this));
            _resizeStartScalePercent = _settings.ScalePercent;
            CaptureMouse();
            Cursor = Cursors.SizeNWSE;
            e.Handled = true;
            return;
        }

        DragMove();
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (_settings.ClickThrough)
        {
            return;
        }

        if (_isResizeDragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndResizeDrag();
                return;
            }

            Point currentScreen = PointToScreen(e.GetPosition(this));
            double delta = ((currentScreen.X - _resizeStartScreenPoint.X) + (currentScreen.Y - _resizeStartScreenPoint.Y)) * 0.45;
            _settings.ScalePercent = (int)Math.Round(Math.Clamp(_resizeStartScalePercent + delta, MinScalePercent, MaxScalePercent));
            return;
        }

        Cursor = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? Cursors.SizeNWSE : Cursors.Arrow;
    }

    private void OnOverlayMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizeDragging)
        {
            EndResizeDrag();
            e.Handled = true;
        }
    }

    private void OnOverlayLostMouseCapture(object sender, MouseEventArgs e)
    {
        EndResizeDrag();
    }

    private void OnOverlayMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_settings.ClickThrough || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        int step = e.Delta > 0 ? 5 : -5;
        _settings.ScalePercent = Math.Clamp(_settings.ScalePercent + step, MinScalePercent, MaxScalePercent);
        e.Handled = true;
    }

    private void EndResizeDrag()
    {
        _isResizeDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        Cursor = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? Cursors.SizeNWSE : Cursors.Arrow;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
