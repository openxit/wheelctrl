using System.Runtime.ExceptionServices;
using System.Windows;
using G29ControlCenter.ViewModels;
using Xunit;

namespace G29ControlCenter.Tests;

public sealed class UiSmokeTests
{
    [Fact]
    public void MainWindow_InstantiatesLaysOutAndExecutesEmergencyStopOnStaThread()
    {
        Exception? capturedException = null;

        var staThread = new Thread(() =>
        {
            try
            {
                Application? app = Application.Current;
                if (app is null)
                {
                    app = new Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown,
                        ThemeMode = ThemeMode.System
                    };
                    var styles = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/WheelCtrl;component/Themes/DiagnosticStyles.xaml", UriKind.Absolute)
                    };
                    app.Resources.MergedDictionaries.Add(styles);
                }

                var window = new MainWindow();
                try
                {
                    var vm = Assert.IsType<MainViewModel>(window.DataContext);
                    Assert.Equal(25, vm.Buttons.Count);

                    var layoutSize = new Size(1560, 880);
                    window.Measure(layoutSize);
                    window.Arrange(new Rect(layoutSize));
                    window.UpdateLayout();

                    Assert.Equal("WheelCtrl", window.Title);

                    vm.InitializeHardware(nint.Zero);

                    vm.SetOperatingRangeCommand.Execute("540");
                    Assert.Equal(540, vm.OperatingRangeDegrees);

                    vm.SetQuickAngleCommand.Execute("90");
                    Assert.True(vm.IsMotorPositioningMode);
                    Assert.Equal(90.0, vm.TargetAngleDegrees, precision: 1);

                    vm.EmergencyStopCommand.Execute(null);
                    Assert.True(vm.IsFreewheelMode);
                    Assert.False(vm.IsMotorPositioningMode);

                    vm.OpenOverlayTabCommand.Execute(null);
                    Assert.True(vm.IsOverlayTabSelected);
                    Assert.False(vm.IsControlsTabSelected);

                    Assert.False(vm.AutoCalibratePedals);
                    Assert.Equal("Stop All Motors", vm.Loc.StopAllMotorsButton);
                    vm.Loc.IsGerman = true;
                    Assert.Equal("Alle Motoren stoppen", vm.Loc.StopAllMotorsButton);
                    vm.Loc.IsEnglish = true;
                    Assert.Equal("Stop All Motors", vm.Loc.StopAllMotorsButton);

                    vm.Shutdown();
                }
                finally
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }
}
