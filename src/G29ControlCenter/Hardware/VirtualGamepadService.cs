using System.Diagnostics;
using System.IO;
using System.Net.Http;
using G29ControlCenter.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace G29ControlCenter.Hardware;

/// <summary>
/// Erzeugt ein echtes virtuelles Gamepad (XInput / DualShock 4 kompatibel) über ViGEmBus
/// und übersetzt G29-Telemetrie direkt auf dem 100-Hz-Hardware-Thread mit minimaler Latenz.
/// </summary>
public sealed class VirtualGamepadService : IDisposable
{
    private const string ViGEmBusSetupUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";

    private readonly object _sync = new();
    private ViGEmClient? _client;
    private IXbox360Controller? _xboxController;
    private IDualShock4Controller? _ds4Controller;
    private VirtualGamepadType _activeType;
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
                VirtualGamepadType requestedType = Config.ControllerType;

                if (_activeType != requestedType)
                {
                    DisconnectControllersUnsafe();
                }

                if (requestedType == VirtualGamepadType.DualShock4)
                {
                    if (_ds4Controller is null)
                    {
                        _ds4Controller = _client.CreateDualShock4Controller();
                        _ds4Controller.AutoSubmitReport = false;
                        _ds4Controller.Connect();
                    }
                    _driverMessage = "Virtual DS4-Compatible Gamepad active";
                }
                else
                {
                    if (_xboxController is null)
                    {
                        _xboxController = _client.CreateXbox360Controller();
                        _xboxController.AutoSubmitReport = false;
                        _xboxController.Connect();
                    }
                    _driverMessage = "Virtual XInput Gamepad active";
                }

                _activeType = requestedType;
                _driverState = VirtualGamepadDriverState.Ready;
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
            if (!_isEnabled || _disposed)
            {
                return;
            }

            try
            {
                if (_activeType != Config.ControllerType && _client is not null)
                {
                    DisconnectControllersUnsafe();
                    if (Config.ControllerType == VirtualGamepadType.DualShock4)
                    {
                        _ds4Controller = _client.CreateDualShock4Controller();
                        _ds4Controller.AutoSubmitReport = false;
                        _ds4Controller.Connect();
                    }
                    else
                    {
                        _xboxController = _client.CreateXbox360Controller();
                        _xboxController.AutoSubmitReport = false;
                        _xboxController.Connect();
                    }
                    _activeType = Config.ControllerType;
                }

                if (_activeType == VirtualGamepadType.DualShock4 && _ds4Controller is not null)
                {
                    ApplyReportToDs4Controller(_ds4Controller, report);
                    _ds4Controller.SubmitReport();
                }
                else if (_xboxController is not null)
                {
                    ApplyReportToController(_xboxController, report);
                    _xboxController.SubmitReport();
                }
                else
                {
                    return;
                }

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

    private static void ApplyReportToDs4Controller(IDualShock4Controller controller, VirtualXbox360Report report)
    {
        controller.SetAxisValue(DualShock4Axis.LeftThumbX, ShortToByteAxis(report.LeftStickX));
        controller.SetAxisValue(DualShock4Axis.LeftThumbY, ShortToByteAxis((short)-Math.Clamp((int)report.LeftStickY, -32767, 32767)));
        controller.SetAxisValue(DualShock4Axis.RightThumbX, ShortToByteAxis(report.RightStickX));
        controller.SetAxisValue(DualShock4Axis.RightThumbY, ShortToByteAxis((short)-Math.Clamp((int)report.RightStickY, -32767, 32767)));
        controller.SetSliderValue(DualShock4Slider.LeftTrigger, report.LeftTrigger);
        controller.SetSliderValue(DualShock4Slider.RightTrigger, report.RightTrigger);

        VirtualXboxButtonMask b = report.Buttons;
        controller.SetButtonState(DualShock4Button.Cross, (b & VirtualXboxButtonMask.A) != 0);
        controller.SetButtonState(DualShock4Button.Circle, (b & VirtualXboxButtonMask.B) != 0);
        controller.SetButtonState(DualShock4Button.Square, (b & VirtualXboxButtonMask.X) != 0);
        controller.SetButtonState(DualShock4Button.Triangle, (b & VirtualXboxButtonMask.Y) != 0);
        controller.SetButtonState(DualShock4Button.ShoulderLeft, (b & VirtualXboxButtonMask.LeftShoulder) != 0);
        controller.SetButtonState(DualShock4Button.ShoulderRight, (b & VirtualXboxButtonMask.RightShoulder) != 0);
        controller.SetButtonState(DualShock4Button.Share, (b & VirtualXboxButtonMask.Back) != 0);
        controller.SetButtonState(DualShock4Button.Options, (b & VirtualXboxButtonMask.Start) != 0);
        controller.SetButtonState(DualShock4Button.ThumbLeft, (b & VirtualXboxButtonMask.LeftThumb) != 0);
        controller.SetButtonState(DualShock4Button.ThumbRight, (b & VirtualXboxButtonMask.RightThumb) != 0);
        controller.SetDPadDirection(ResolveDs4DPad(b));
    }

    private static byte ShortToByteAxis(short value)
    {
        int scaled = ((value + 32768) * 255) / 65535;
        return (byte)Math.Clamp(scaled, 0, 255);
    }

    private static DualShock4DPadDirection ResolveDs4DPad(VirtualXboxButtonMask b)
    {
        bool up = (b & VirtualXboxButtonMask.Up) != 0;
        bool down = (b & VirtualXboxButtonMask.Down) != 0;
        bool left = (b & VirtualXboxButtonMask.Left) != 0;
        bool right = (b & VirtualXboxButtonMask.Right) != 0;

        if (up && right) return DualShock4DPadDirection.Northeast;
        if (down && right) return DualShock4DPadDirection.Southeast;
        if (down && left) return DualShock4DPadDirection.Southwest;
        if (up && left) return DualShock4DPadDirection.Northwest;
        if (up) return DualShock4DPadDirection.North;
        if (right) return DualShock4DPadDirection.East;
        if (down) return DualShock4DPadDirection.South;
        if (left) return DualShock4DPadDirection.West;
        return DualShock4DPadDirection.None;
    }

    private void DisconnectControllersUnsafe()
    {
        if (_xboxController is not null)
        {
            try { _xboxController.Disconnect(); } catch { }
            _xboxController = null;
        }
        if (_ds4Controller is not null)
        {
            try { _ds4Controller.Disconnect(); } catch { }
            _ds4Controller = null;
        }
    }

    private void DisconnectUnsafe()
    {
        DisconnectControllersUnsafe();

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
