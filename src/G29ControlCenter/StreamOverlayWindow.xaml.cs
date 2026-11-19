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

    private readonly OverlaySettings _settings;

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
        if (!_settings.ClickThrough && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
