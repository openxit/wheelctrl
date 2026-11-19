using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using G29ControlCenter.ViewModels;

namespace G29ControlCenter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) => ViewModel?.SetWindowFocused(true);
        Deactivated += (_, _) => ViewModel?.SetWindowFocused(false);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint hwnd = new WindowInteropHelper(this).Handle;
        ViewModel?.InitializeHardware(hwnd);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ViewModel?.Shutdown();
        base.OnClosing(e);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Escape)
        {
            ViewModel?.EmergencyStopCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnWheelControlDraggedAngle(object? sender, double angleDegrees)
    {
        ViewModel?.HandleWheelDraggedAngle(angleDegrees);
    }

    private void OnTargetInputGotFocus(object sender, RoutedEventArgs e)
    {
        ViewModel?.SetTargetInputFocus(true);
    }

    private void OnTargetInputLostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel?.SetTargetInputFocus(false);
        ViewModel?.ApplyTargetAngleCommand.Execute(null);
    }

    private void OnTargetInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel?.ApplyTargetAngleCommand.Execute(null);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
