using System.Diagnostics;
using System.IO;
using System.Net.Http;
using G29ControlCenter.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace G29ControlCenter.Hardware;

/// <summary>
/// Erzeugt ein echtes virtuelles Xbox-360-Gamepad über ViGEmBus und übersetzt
/// G29-Telemetrie direkt auf dem 100-Hz-Hardware-Thread mit minimaler Latenz.
/// </summary>
public sealed class VirtualGamepadService : IDisposable
{
    private const string ViGEmBusSetupUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";

    private readonly object _sync = new();
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private bool _isEnabled;
    private bool _disposed;

    private VirtualGamepadDriverState _driverState = VirtualGamepadDriverState.NotChecked;
    private string _driverMessage = string.Empty;
    private VirtualXbox360Report _lastReport;
    private double _lastTranslateLatencyMs;
    private double _avgTranslateLatencyMs;
    private long _reportsSentCount;

    public VirtualGamepadService(VirtualGamepadMappingConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public VirtualGamepadMappingConfig Config { get; }

    public bool IsEnabled
    {
        get { lock (_sync) return _isEnabled; }
    }

    public VirtualGamepadDriverState DriverState
    {
        get { lock (_sync) return _driverState; }
    }

    public string DriverMessage
    {
        get { lock (_sync) return _driverMessage; }
    }

    public VirtualXbox360Report LastReport
    {
        get { lock (_sync) return _lastReport; }
    }

    public double LastTranslateLatencyMs
    {
        get { lock (_sync) return _lastTranslateLatencyMs; }
    }

    public double AverageTranslateLatencyMs
    {
        get { lock (_sync) return _avgTranslateLatencyMs; }
    }

    public long ReportsSentCount
    {
        get { lock (_sync) return _reportsSentCount; }
    }

    public bool CheckDriverStatus()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return false;
            }

            if (_client is not null)
            {
                _driverState = VirtualGamepadDriverState.Ready;
                _driverMessage = "ViGEmBus ready";
                return true;
            }

            try
            {
                using var probe = new ViGEmClient();
                _driverState = VirtualGamepadDriverState.Ready;
                _driverMessage = "ViGEmBus ready";
                return true;
            }
            catch (VigemBusNotFoundException)
            {
                _driverState = VirtualGamepadDriverState.DriverMissing;
                _driverMessage = "ViGEmBus kernel driver not installed";
                return false;
            }
            catch (Exception ex)
            {
                _driverState = VirtualGamepadDriverState.Error;
                _driverMessage = ex.Message;
                return false;
            }
        }
    }

    public bool SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!enabled)
            {
                DisconnectUnsafe();
                _isEnabled = false;
                return true;
            }

            try
            {
                _client ??= new ViGEmClient();
                if (_controller is null)
                {
                    _controller = _client.CreateXbox360Controller();
                    _controller.AutoSubmitReport = false;
                    _controller.Connect();
                }

                _driverState = VirtualGamepadDriverState.Ready;
                _driverMessage = "Virtual Xbox 360 Controller active";
                _isEnabled = true;
                return true;
            }
            catch (VigemBusNotFoundException)
            {
                DisconnectUnsafe();
                _driverState = VirtualGamepadDriverState.DriverMissing;
                _driverMessage = "ViGEmBus kernel driver not installed";
                _isEnabled = false;
                return false;
            }
            catch (Exception ex)
            {
                DisconnectUnsafe();
                _driverState = VirtualGamepadDriverState.Error;
                _driverMessage = ex.Message;
                _isEnabled = false;
                return false;
            }
        }
    }

    /// <summary>
    /// Wird direkt vom 100-Hz-Hardware-Thread aufgerufen (0 ms UI-Verzögerung).
    /// </summary>
    public void ProcessSnapshot(WheelTelemetrySnapshot snapshot)
    {
        long startTicks = Stopwatch.GetTimestamp();
        VirtualXbox360Report report = VirtualGamepadReportMapper.Map(snapshot, Config);

        lock (_sync)
        {
            _lastReport = report;
            if (!_isEnabled || _controller is null || _disposed)
            {
                return;
            }

            try
            {
                ApplyReportToController(_controller, report);
                _controller.SubmitReport();

                double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
                _lastTranslateLatencyMs = elapsedMs;
                _avgTranslateLatencyMs = _reportsSentCount == 0
                    ? elapsedMs
                    : (_avgTranslateLatencyMs * 0.90) + (elapsedMs * 0.10);
                _reportsSentCount++;
            }
            catch (Exception ex)
            {
                _driverState = VirtualGamepadDriverState.Error;
                _driverMessage = ex.Message;
                _isEnabled = false;
                DisconnectUnsafe();
            }
        }
    }

    public async Task<(bool Success, string Message)> InstallViGEmBusDriverAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "WheelCtrl");
            Directory.CreateDirectory(tempDir);
            string setupPath = Path.Combine(tempDir, "ViGEmBus_Setup.exe");

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
            {
                using HttpResponseMessage response = await http.GetAsync(ViGEmBusSetupUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using FileStream fs = File.Create(setupPath);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            using Process? proc = Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = "/quiet /norestart",
                UseShellExecute = true,
                Verb = "runas"
            });

            if (proc is null)
            {
                return (false, "Could not start ViGEmBus installer.");
            }

            await proc.WaitForExitAsync(cancellationToken);
            bool ready = CheckDriverStatus();
            return ready
                ? (true, "ViGEmBus installed and ready!")
                : (false, $"Installer exited with code {proc.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            return (false, "Installation cancelled.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void ApplyReportToController(IXbox360Controller controller, VirtualXbox360Report report)
    {
        controller.SetAxisValue(Xbox360Axis.LeftThumbX, report.LeftStickX);
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, report.LeftStickY);
        controller.SetAxisValue(Xbox360Axis.RightThumbX, report.RightStickX);
        controller.SetAxisValue(Xbox360Axis.RightThumbY, report.RightStickY);
        controller.SetSliderValue(Xbox360Slider.LeftTrigger, report.LeftTrigger);
        controller.SetSliderValue(Xbox360Slider.RightTrigger, report.RightTrigger);

        VirtualXboxButtonMask b = report.Buttons;
        controller.SetButtonState(Xbox360Button.A, (b & VirtualXboxButtonMask.A) != 0);
        controller.SetButtonState(Xbox360Button.B, (b & VirtualXboxButtonMask.B) != 0);
        controller.SetButtonState(Xbox360Button.X, (b & VirtualXboxButtonMask.X) != 0);
        controller.SetButtonState(Xbox360Button.Y, (b & VirtualXboxButtonMask.Y) != 0);
        controller.SetButtonState(Xbox360Button.LeftShoulder, (b & VirtualXboxButtonMask.LeftShoulder) != 0);
        controller.SetButtonState(Xbox360Button.RightShoulder, (b & VirtualXboxButtonMask.RightShoulder) != 0);
        controller.SetButtonState(Xbox360Button.Back, (b & VirtualXboxButtonMask.Back) != 0);
        controller.SetButtonState(Xbox360Button.Start, (b & VirtualXboxButtonMask.Start) != 0);
        controller.SetButtonState(Xbox360Button.LeftThumb, (b & VirtualXboxButtonMask.LeftThumb) != 0);
        controller.SetButtonState(Xbox360Button.RightThumb, (b & VirtualXboxButtonMask.RightThumb) != 0);
        controller.SetButtonState(Xbox360Button.Up, (b & VirtualXboxButtonMask.Up) != 0);
        controller.SetButtonState(Xbox360Button.Down, (b & VirtualXboxButtonMask.Down) != 0);
        controller.SetButtonState(Xbox360Button.Left, (b & VirtualXboxButtonMask.Left) != 0);
        controller.SetButtonState(Xbox360Button.Right, (b & VirtualXboxButtonMask.Right) != 0);
    }

    private void DisconnectUnsafe()
    {
        if (_controller is not null)
        {
            try
            {
                _controller.Disconnect();
            }
            catch
            {
                // Ignore disconnect errors
            }
            _controller = null;
        }

        if (_client is not null)
        {
            try
            {
                _client.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
            _client = null;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _isEnabled = false;
            DisconnectUnsafe();
        }
    }
}
