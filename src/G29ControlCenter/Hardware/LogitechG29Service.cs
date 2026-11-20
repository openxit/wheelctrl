using System.Diagnostics;
using System.IO;
using System.Text;
using G29ControlCenter.Models;

namespace G29ControlCenter.Hardware;

/// <summary>
/// Echtzeit-Hardware-Service für das physische Logitech G29 Driving Force Lenkrad
/// über die offizielle Logitech G HUB x64-Engine (100-Hz-Regelschleife).
/// </summary>
public sealed class LogitechG29Service : IWheelHardwareService
{
    private const int MaxControllerScanCount = 4;
    private const int TargetLoopPeriodMs = 10;

    private readonly object _sdkLock = new();
    private readonly object _stateLock = new();
    private readonly PidController _pidController = new();
    private readonly PedalCalibrationSet _pedalCalibration = new();

    private CancellationTokenSource? _loopCts;
    private Thread? _loopThread;
    private bool _timerPeriodActive;
    private bool _sdkInitialized;
    private bool _sdkBinaryLoaded;
    private int _wheelIndex = -1;
    private nint _windowHandle;
    private bool _disposed;
    private long _lastHotplugProbeTimestamp;

    private WheelTelemetrySnapshot _latestSnapshot = new();

    // Konfigurierte Steuerparameter
    private ControlMode _controlMode = ControlMode.TelemetryFreewheel;
    private int _operatingRangeDegrees = 900;
    private double _targetAngleDegrees;
    private bool _freewheelGentleDampingEnabled;
    private int _freewheelDampingPercent = 15;
    private FfbEffectState _ffbEffects = new();
    private PedalCouplingMode _pedalCouplingMode = PedalCouplingMode.None;
    private RpmLedMode _rpmLedMode = RpmLedMode.Manual;
    private double _manualRpmPercent;
    private bool _isWindowFocused = true;
    private bool _combinedPedalsWarning;
    private bool _hasForceFeedback;
    private string _deviceName = "Suche Logitech G29...";
    private string _loadedSdkPath = string.Empty;
    private HardwareConnectionState _connectionState = HardwareConnectionState.Initializing;
    private string _statusMessage = "Initialisiere Logitech SDK...";

    // Schutz gegen uninitialisierte Nullwerte vor erstem HID-Pedalbericht
    private bool _hasReceivedValidPedalReport;

    // Hardware-Effekt-Cache (minimiert redundante DirectInput-Aufrufe)
    private int _lastConstantForceSent = int.MinValue;
    private bool _lastSpringActive;
    private int _lastSpringOffset = int.MinValue;
    private int _lastSpringSaturation = int.MinValue;
    private int _lastSpringCoefficient = int.MinValue;
    private bool _lastDamperActive;
    private int _lastDamperCoefficient = int.MinValue;
    private bool _lastBumpyActive;
    private int _lastBumpyMagnitude = int.MinValue;
    private bool _lastDirtActive;
    private int _lastDirtMagnitude = int.MinValue;
    private double _lastRpmLedSent = -1.0;

    // 24V-Motor-Reaktions- und Polaritäts-Watchdog
    private MotorPowerState _motorPowerState = MotorPowerState.Disconnected;
    private string _motorStatusHint = "Nicht verbunden";
    private bool _forceWatchActive;
    private double _forceWatchStartAngle;
    private long _forceWatchStartTimestamp;
    private bool _polarityWatchActive;
    private double _polarityWatchStartAbsError;
    private long _polarityWatchStartTimestamp;
    private bool _polarityWarningActive;

    public event Action<WheelTelemetrySnapshot>? TelemetryUpdated;

    public WheelTelemetrySnapshot LatestSnapshot => Volatile.Read(ref _latestSnapshot);

    public void Initialize(nint windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _windowHandle = windowHandle;
        EnsureLogitechGHubRunning();
        ProbeAndInitializeHardware();
        StartControlLoop();
    }

    public void TryReconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureLogitechGHubRunning();

        lock (_sdkLock)
        {
            if (_sdkInitialized)
            {
                StopAllHardwareEffectsUnsafe();
                try
                {
                    LogitechNative.LogiSteeringShutdown();
                }
                catch
                {
                    // Treiber-Fehler beim Reconnect ignorieren
                }

                _sdkInitialized = false;
            }

            _wheelIndex = -1;
        }

        _hasReceivedValidPedalReport = false;
        ResetEffectCache();
        ProbeAndInitializeHardware();
    }

    private static void EnsureLogitechGHubRunning()
    {
        try
        {
            if (Process.GetProcessesByName("lghub_agent").Length > 0)
            {
                return;
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string lgHubExe = Path.Combine(programFiles, "LGHUB", "lghub.exe");
            if (!File.Exists(lgHubExe))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = lgHubExe,
                Arguments = "--background",
                UseShellExecute = true
            });

            // Kurz auf G HUB Backend-Initialisierung warten
            for (int i = 0; i < 8; i++)
            {
                if (Process.GetProcessesByName("lghub_agent").Length > 0)
                {
                    break;
                }

                Thread.Sleep(100);
            }
        }
        catch
        {
            // Falls G HUB nicht automatisch gestartet werden kann, normal weiter initialisieren
        }
    }

    private void ProbeAndInitializeHardware()
    {
        if (!LogitechNative.EnsureLoaded(out string loadMessage))
        {
            _sdkBinaryLoaded = false;
            lock (_stateLock)
            {
                _connectionState = HardwareConnectionState.DriverUnavailable;
                _deviceName = "SDK-Treiber nicht verfügbar";
                _loadedSdkPath = string.Empty;
                _statusMessage = loadMessage;
                _hasForceFeedback = false;
                _combinedPedalsWarning = false;
                _motorPowerState = MotorPowerState.Disconnected;
                _motorStatusHint = "Logitech G HUB / SDK-Treiber prüfen";
            }

            PublishEmptySnapshot(100.0);
            return;
        }

        _sdkBinaryLoaded = true;
        _loadedSdkPath = LogitechNative.LoadedLibraryPath ?? loadMessage;

        lock (_sdkLock)
        {
            try
            {
                bool initOk = _windowHandle != nint.Zero
                    ? LogitechNative.LogiSteeringInitializeWithWindow(true, _windowHandle)
                    : LogitechNative.LogiSteeringInitialize(true);

                _sdkInitialized = initOk;
                if (!initOk)
                {
                    lock (_stateLock)
                    {
                        _connectionState = HardwareConnectionState.WaitingForDevice;
                        _deviceName = "G29 noch nicht bereit";
                        _statusMessage = "DirectInput-Initialisierung fehlgeschlagen — bitte Fokus & G HUB prüfen und 'Neu verbinden' klicken.";
                        _hasForceFeedback = false;
                        _motorPowerState = MotorPowerState.Disconnected;
                        _motorStatusHint = "Keine SDK-Initialisierung";
                    }

                    PublishEmptySnapshot(100.0);
                    return;
                }

                // Mehrere kurze Update-Ticks geben G HUB / DirectInput Zeit zur Enumeration
                int foundIndex = -1;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    LogitechNative.LogiUpdate();
                    foundIndex = FindConnectedWheelIndexUnsafe();
                    if (foundIndex >= 0)
                    {
                        break;
                    }

                    Thread.Sleep(30);
                }

                if (foundIndex < 0)
                {
                    _wheelIndex = -1;
                    lock (_stateLock)
                    {
                        _connectionState = HardwareConnectionState.WaitingForDevice;
                        _deviceName = "Kein G29 erkannt";
                        _statusMessage = "SDK aktiv, warte auf G29 (USB-Kabel, 24V-Netzteil & PS4/PS3-Schalter oben auf PS4 prüfen).";
                        _hasForceFeedback = false;
                        _combinedPedalsWarning = false;
                        _motorPowerState = MotorPowerState.Disconnected;
                        _motorStatusHint = "Kein Gerät verbunden";
                    }

                    PublishEmptySnapshot(100.0);
                    return;
                }

                AttachConnectedWheelUnsafe(foundIndex);
            }
            catch (Exception ex)
            {
                _sdkInitialized = false;
                _wheelIndex = -1;
                lock (_stateLock)
                {
                    _connectionState = HardwareConnectionState.DriverUnavailable;
                    _deviceName = "SDK-Fehler";
                    _statusMessage = $"Fehler bei SDK-Initialisierung: {ex.Message}";
                    _hasForceFeedback = false;
                    _motorPowerState = MotorPowerState.Disconnected;
                    _motorStatusHint = "Initialisierungsfehler";
                }

                PublishEmptySnapshot(100.0);
            }
        }
    }

    private int FindConnectedWheelIndexUnsafe()
    {
        int fallbackConnected = -1;

        for (int i = 0; i < MaxControllerScanCount; i++)
        {
            if (!LogitechNative.LogiIsConnected(i))
            {
                continue;
            }

            if (fallbackConnected < 0)
            {
                fallbackConnected = i;
            }

            var nameBuf = new StringBuilder(256);
            if (LogitechNative.LogiGetFriendlyProductName(i, nameBuf, nameBuf.Capacity))
            {
                string candidate = nameBuf.ToString();
                if (candidate.Contains("G29", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains("Driving Force", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains("Logitech", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return fallbackConnected;
    }

    private void AttachConnectedWheelUnsafe(int index)
    {
        _wheelIndex = index;
        _hasReceivedValidPedalReport = false;
        ResetEffectCache();

        var nameBuffer = new StringBuilder(256);
        string productName = LogitechNative.LogiGetFriendlyProductName(index, nameBuffer, nameBuffer.Capacity)
            ? nameBuffer.ToString().Trim()
            : "Logitech G29 Driving Force";

        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = "Logitech G29 Driving Force";
        }

        bool hasFfb = LogitechNative.LogiHasForceFeedback(index);
        LogitechNative.LogiSetOperatingRange(index, _operatingRangeDegrees);

        bool combinedPedals = false;
        var props = new LogiControllerPropertiesData();
        if (LogitechNative.LogiGetCurrentControllerProperties(index, ref props))
        {
            combinedPedals = props.combinePedals;
        }

        lock (_stateLock)
        {
            _connectionState = HardwareConnectionState.Connected;
            _deviceName = productName;
            _hasForceFeedback = hasFfb;
            _combinedPedalsWarning = combinedPedals;
            _statusMessage = combinedPedals
                ? "G29 Verbunden (Hinweis: 'Kombinierte Pedale' in G HUB deaktivieren)"
                : $"G29 Verbunden (Index {index}, SDK: {Path.GetFileName(_loadedSdkPath)})";
            _motorPowerState = hasFfb ? MotorPowerState.UnknownReady : MotorPowerState.NoResponseCheck24V;
            _motorStatusHint = hasFfb
                ? "Bereit (warte auf Motoransteuerung)"
                : "Kein Force-Feedback gemeldet — 24V-Netzteil prüfen";
        }
    }

    private void StartControlLoop()
    {
        if (_loopThread is not null)
        {
            return;
        }

        try
        {
            _timerPeriodActive = LogitechNative.timeBeginPeriod(1) == 0;
        }
        catch
        {
            _timerPeriodActive = false;
        }

        _loopCts = new CancellationTokenSource();
        CancellationToken token = _loopCts.Token;

        _loopThread = new Thread(() => RunControlLoop(token))
        {
            Name = "G29-100Hz-ControlLoop",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _loopThread.Start();
    }

    private void RunControlLoop(CancellationToken token)
    {
        long frequency = Stopwatch.Frequency;
        long previousTimestamp = Stopwatch.GetTimestamp();
        double smoothedHz = 100.0;

        while (!token.IsCancellationRequested)
        {
            long tickStart = Stopwatch.GetTimestamp();
            double dt = (double)(tickStart - previousTimestamp) / frequency;
            previousTimestamp = tickStart;

            if (dt > 0.0005 && dt < 0.25)
            {
                double instantHz = 1.0 / dt;
                smoothedHz = (0.15 * instantHz) + (0.85 * smoothedHz);
            }

            StepHardwareTick(dt, smoothedHz);

            double elapsedMs = (double)(Stopwatch.GetTimestamp() - tickStart) * 1000.0 / frequency;
            int sleepMs = (int)Math.Floor(TargetLoopPeriodMs - elapsedMs);
            if (sleepMs > 1)
            {
                Thread.Sleep(sleepMs - 1);
            }

            while ((double)(Stopwatch.GetTimestamp() - tickStart) * 1000.0 / frequency < TargetLoopPeriodMs)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Thread.SpinWait(64);
            }
        }
    }

    private void StepHardwareTick(double dt, double measuredHz)
    {
        if (!_sdkBinaryLoaded || !_sdkInitialized)
        {
            PublishEmptySnapshot(measuredHz);
            return;
        }

        ControlMode mode;
        int rangeDeg;
        double manualTargetDeg;
        bool gentleDamping;
        int gentleDampingPct;
        FfbEffectState effects;
        PedalCouplingMode couplingMode;
        RpmLedMode ledMode;
        double manualRpmPct;
        bool windowFocused;

        lock (_stateLock)
        {
            mode = _controlMode;
            rangeDeg = _operatingRangeDegrees;
            manualTargetDeg = _targetAngleDegrees;
            gentleDamping = _freewheelGentleDampingEnabled;
            gentleDampingPct = _freewheelDampingPercent;
            effects = _ffbEffects;
            couplingMode = _pedalCouplingMode;
            ledMode = _rpmLedMode;
            manualRpmPct = _manualRpmPercent;
            windowFocused = _isWindowFocused;
        }

        double halfRange = rangeDeg / 2.0;
        DIJOYSTATE2ENGINES state = default;
        bool hasState = false;
        int activeIndex;

        lock (_sdkLock)
        {
            bool updated = LogitechNative.LogiUpdate();
            activeIndex = _wheelIndex;

            if (!updated || activeIndex < 0 || !LogitechNative.LogiIsConnected(activeIndex))
            {
                if (activeIndex >= 0)
                {
                    StopAllHardwareEffectsUnsafe();
                    _wheelIndex = -1;
                    _hasReceivedValidPedalReport = false;
                    lock (_stateLock)
                    {
                        _connectionState = HardwareConnectionState.WaitingForDevice;
                        _deviceName = "G29 getrennt";
                        _statusMessage = "Verbindung zum G29 unterbrochen — warte auf erneute Verbindung...";
                        _hasForceFeedback = false;
                        _motorPowerState = MotorPowerState.Disconnected;
                        _motorStatusHint = "Gerät getrennt";
                    }
                }

                // Alle ~500 ms auf neu angeschlossenes / verzögert initialisiertes G29 scannen
                long now = Stopwatch.GetTimestamp();
                double msSinceProbe = (double)(now - _lastHotplugProbeTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (msSinceProbe >= 500.0)
                {
                    _lastHotplugProbeTimestamp = now;
                    int found = FindConnectedWheelIndexUnsafe();
                    if (found >= 0)
                    {
                        AttachConnectedWheelUnsafe(found);
                        activeIndex = found;
                    }
                }
            }

            if (activeIndex >= 0 && LogitechNative.LogiIsConnected(activeIndex))
            {
                hasState = LogitechNative.TryGetState(activeIndex, out state);
            }
        }

        if (activeIndex < 0 || !hasState)
        {
            _pidController.Reset();
            PublishEmptySnapshot(measuredHz);
            return;
        }

        int rawX = Math.Clamp(state.lX, -32768, 32767);
        double actualPercent = RawXToPercent(rawX);
        double actualAngleDeg = (actualPercent / 100.0) * halfRange;

        int gasRaw = state.lY;
        int brakeRaw = state.lRz;
        int clutchRaw = (state.rglSlider is { Length: > 0 }) ? state.rglSlider[0] : PedalAxisCalibrator.NominalReleasedRaw;

        double gasPercent;
        double brakePercent;
        double clutchPercent;

        if (!_hasReceivedValidPedalReport && gasRaw == 0 && brakeRaw == 0 && clutchRaw == 0)
        {
            // DirectInput liefert vor dem ersten HID-Report oft 0 auf allen Pedalachsen
            gasPercent = 0.0;
            brakePercent = 0.0;
            clutchPercent = 0.0;
            gasRaw = PedalAxisCalibrator.NominalReleasedRaw;
            brakeRaw = PedalAxisCalibrator.NominalReleasedRaw;
            clutchRaw = PedalAxisCalibrator.NominalReleasedRaw;
        }
        else
        {
            _hasReceivedValidPedalReport = true;
            gasPercent = _pedalCalibration.Gas.Normalize(gasRaw);
            brakePercent = _pedalCalibration.Brake.Normalize(brakeRaw);
            clutchPercent = _pedalCalibration.Clutch.Normalize(clutchRaw);
        }

        double effectiveTargetDeg = ResolveEffectiveTargetAngle(mode, manualTargetDeg, halfRange, couplingMode, gasPercent);
        int commandedConstantForce = 0;

        if (windowFocused && mode == ControlMode.MotorPositioning)
        {
            commandedConstantForce = _pidController.Compute(effectiveTargetDeg, actualAngleDeg, dt);
        }
        else
        {
            _pidController.Reset();
        }

        int effectiveBumpyPct = ResolveEffectiveBumpyPercent(effects, couplingMode, gasPercent);
        bool effectiveBumpyEnabled = effects.BumpyRoadEnabled || (couplingMode == PedalCouplingMode.GasToBumpyVibration && gasPercent > 1.0);
        bool effectiveDamperEnabled = effects.DamperEnabled || (mode == ControlMode.TelemetryFreewheel && gentleDamping);
        int effectiveDamperPct = effects.DamperEnabled ? effects.DamperCoefficientPercent : gentleDampingPct;

        double rpmNormalized = ResolveRpmNormalized(ledMode, manualRpmPct, gasPercent);

        lock (_sdkLock)
        {
            if (windowFocused)
            {
                ApplyConstantForceHardwareUnsafe(activeIndex, commandedConstantForce);
                ApplySecondaryEffectsHardwareUnsafe(
                    activeIndex,
                    effects,
                    effectiveDamperEnabled,
                    effectiveDamperPct,
                    effectiveBumpyEnabled,
                    effectiveBumpyPct);
            }
            else
            {
                StopAllHardwareEffectsUnsafe();
            }

            if (Math.Abs(rpmNormalized - _lastRpmLedSent) >= 0.01)
            {
                // 0.15f..1.12f: Rot erst ab ~90 % (nicht schon bei 70 %), kein Redline-Blinken bei 100 %
                LogitechNative.LogiPlayLeds(activeIndex, (float)rpmNormalized, 0.15f, 1.12f);
                _lastRpmLedSent = rpmNormalized;
            }
        }

        EvaluateMotorWatchdog(windowFocused, mode, commandedConstantForce, effectiveTargetDeg, actualAngleDeg);

        bool[] buttons = ExtractButtons(state.rgbButtons);
        uint povRaw = (state.rgdwPOV is { Length: > 0 }) ? state.rgdwPOV[0] : uint.MaxValue;

        PublishSnapshot(
            activeIndex,
            buttons,
            povRaw,
            rawX,
            actualAngleDeg,
            actualPercent,
            effectiveTargetDeg,
            gasPercent,
            gasRaw,
            brakePercent,
            brakeRaw,
            clutchPercent,
            clutchRaw,
            commandedConstantForce,
            rpmNormalized,
            measuredHz);
    }

    private void ApplyConstantForceHardwareUnsafe(int index, int forcePercent)
    {
        if (forcePercent == 0)
        {
            if (_lastConstantForceSent != 0)
            {
                LogitechNative.LogiStopConstantForce(index);
                _lastConstantForceSent = 0;
            }
        }
        else if (forcePercent != _lastConstantForceSent)
        {
            LogitechNative.LogiPlayConstantForce(index, forcePercent);
            _lastConstantForceSent = forcePercent;
        }
    }

    private void ApplySecondaryEffectsHardwareUnsafe(
        int index,
        FfbEffectState effects,
        bool effectiveDamperEnabled,
        int effectiveDamperPct,
        bool effectiveBumpyEnabled,
        int effectiveBumpyPct)
    {
        if (effects.SpringEnabled)
        {
            if (!_lastSpringActive ||
                effects.SpringOffsetPercent != _lastSpringOffset ||
                effects.SpringSaturationPercent != _lastSpringSaturation ||
                effects.SpringCoefficientPercent != _lastSpringCoefficient)
            {
                LogitechNative.LogiPlaySpringForce(
                    index,
                    effects.SpringOffsetPercent,
                    effects.SpringSaturationPercent,
                    effects.SpringCoefficientPercent);
                _lastSpringActive = true;
                _lastSpringOffset = effects.SpringOffsetPercent;
                _lastSpringSaturation = effects.SpringSaturationPercent;
                _lastSpringCoefficient = effects.SpringCoefficientPercent;
            }
        }
        else if (_lastSpringActive)
        {
            LogitechNative.LogiStopSpringForce(index);
            _lastSpringActive = false;
        }

        if (effectiveDamperEnabled)
        {
            if (!_lastDamperActive || effectiveDamperPct != _lastDamperCoefficient)
            {
                LogitechNative.LogiPlayDamperForce(index, effectiveDamperPct);
                _lastDamperActive = true;
                _lastDamperCoefficient = effectiveDamperPct;
            }
        }
        else if (_lastDamperActive)
        {
            LogitechNative.LogiStopDamperForce(index);
            _lastDamperActive = false;
        }

        if (effectiveBumpyEnabled)
        {
            if (!_lastBumpyActive || effectiveBumpyPct != _lastBumpyMagnitude)
            {
                LogitechNative.LogiPlayBumpyRoadEffect(index, effectiveBumpyPct);
                _lastBumpyActive = true;
                _lastBumpyMagnitude = effectiveBumpyPct;
            }
        }
        else if (_lastBumpyActive)
        {
            LogitechNative.LogiStopBumpyRoadEffect(index);
            _lastBumpyActive = false;
        }

        if (effects.DirtRoadEnabled)
        {
            if (!_lastDirtActive || effects.DirtRoadMagnitudePercent != _lastDirtMagnitude)
            {
                LogitechNative.LogiPlayDirtRoadEffect(index, effects.DirtRoadMagnitudePercent);
                _lastDirtActive = true;
                _lastDirtMagnitude = effects.DirtRoadMagnitudePercent;
            }
        }
        else if (_lastDirtActive)
        {
            LogitechNative.LogiStopDirtRoadEffect(index);
            _lastDirtActive = false;
        }
    }

    private void StopAllHardwareEffectsUnsafe()
    {
        if (!_sdkInitialized || _wheelIndex < 0)
        {
            ResetEffectCache();
            return;
        }

        try
        {
            LogitechNative.LogiStopConstantForce(_wheelIndex);
            LogitechNative.LogiStopSpringForce(_wheelIndex);
            LogitechNative.LogiStopDamperForce(_wheelIndex);
            LogitechNative.LogiStopBumpyRoadEffect(_wheelIndex);
            LogitechNative.LogiStopDirtRoadEffect(_wheelIndex);
        }
        catch
        {
            // Treiber-Ausnahmen beim Herunterfahren / Trennen ignorieren
        }

        ResetEffectCache();
    }

    private void ResetEffectCache()
    {
        _lastConstantForceSent = int.MinValue;
        _lastSpringActive = false;
        _lastDamperActive = false;
        _lastBumpyActive = false;
        _lastDirtActive = false;
        _lastRpmLedSent = -1.0;
    }

    private void EvaluateMotorWatchdog(
        bool windowFocused,
        ControlMode mode,
        int commandedForce,
        double targetAngleDeg,
        double actualAngleDeg)
    {
        long now = Stopwatch.GetTimestamp();

        if (!windowFocused || Math.Abs(commandedForce) < 20)
        {
            _forceWatchActive = false;
            _polarityWatchActive = false;
            return;
        }

        if (!_forceWatchActive)
        {
            _forceWatchActive = true;
            _forceWatchStartAngle = actualAngleDeg;
            _forceWatchStartTimestamp = now;
        }
        else
        {
            double movedDeg = Math.Abs(actualAngleDeg - _forceWatchStartAngle);
            double elapsedMs = (double)(now - _forceWatchStartTimestamp) * 1000.0 / Stopwatch.Frequency;

            if (movedDeg >= 0.3)
            {
                lock (_stateLock)
                {
                    _motorPowerState = MotorPowerState.Verified24V;
                    _motorStatusHint = "24V OK (Motorreaktion erkannt)";
                }
            }
            else if (elapsedMs >= 600.0)
            {
                lock (_stateLock)
                {
                    _motorPowerState = MotorPowerState.NoResponseCheck24V;
                    _motorStatusHint = "Keine Motorreaktion — 24V-Netzteil prüfen!";
                }
            }
        }

        if (mode == ControlMode.MotorPositioning)
        {
            double absError = Math.Abs(targetAngleDeg - actualAngleDeg);
            if (!_polarityWatchActive)
            {
                _polarityWatchActive = true;
                _polarityWatchStartAbsError = absError;
                _polarityWatchStartTimestamp = now;
            }
            else
            {
                double elapsedMs = (double)(now - _polarityWatchStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs >= 400.0 && (absError - _polarityWatchStartAbsError) > 15.0)
                {
                    lock (_stateLock)
                    {
                        _polarityWarningActive = true;
                    }
                }
                else if ((_polarityWatchStartAbsError - absError) > 3.0)
                {
                    lock (_stateLock)
                    {
                        _polarityWarningActive = false;
                    }
                    _polarityWatchStartAbsError = absError;
                    _polarityWatchStartTimestamp = now;
                }
            }
        }
        else
        {
            _polarityWatchActive = false;
        }
    }

    private void PublishEmptySnapshot(double measuredHz)
    {
        PublishSnapshot(
            -1,
            new bool[WheelTelemetrySnapshot.ButtonCount],
            uint.MaxValue,
            0,
            0.0,
            0.0,
            _targetAngleDegrees,
            0.0,
            PedalAxisCalibrator.NominalReleasedRaw,
            0.0,
            PedalAxisCalibrator.NominalReleasedRaw,
            0.0,
            PedalAxisCalibrator.NominalReleasedRaw,
            0,
            0.0,
            measuredHz);
    }

    private void PublishSnapshot(
        int controllerIndex,
        bool[] buttons,
        uint povRaw,
        int rawX,
        double angleDeg,
        double wheelPercent,
        double targetAngleDeg,
        double gasPercent,
        int gasRaw,
        double brakePercent,
        int brakeRaw,
        double clutchPercent,
        int clutchRaw,
        int commandedForce,
        double rpmNormalized,
        double actualLoopHz)
    {
        WheelTelemetrySnapshot snapshot;
        lock (_stateLock)
        {
            snapshot = new WheelTelemetrySnapshot
            {
                ConnectionState = _connectionState,
                ControllerIndex = controllerIndex,
                LoadedSdkPath = _loadedSdkPath,
                DeviceName = _deviceName,
                StatusMessage = _statusMessage,
                IsWindowFocused = _isWindowFocused,
                HasForceFeedback = _hasForceFeedback,
                MotorPowerState = _motorPowerState,
                MotorStatusHint = _motorStatusHint,
                PolarityWarningActive = _polarityWarningActive,
                OperatingRangeDegrees = _operatingRangeDegrees,
                WheelAngleDegrees = angleDeg,
                WheelPercent = wheelPercent,
                WheelRawX = rawX,
                TargetAngleDegrees = targetAngleDeg,
                GasPercent = gasPercent,
                GasRaw = gasRaw,
                BrakePercent = brakePercent,
                BrakeRaw = brakeRaw,
                ClutchPercent = clutchPercent,
                ClutchRaw = clutchRaw,
                Buttons = buttons,
                PovRaw = povRaw,
                DPadLabel = WheelTelemetrySnapshot.DecodeDPadLabel(povRaw),
                ShifterGear = WheelTelemetrySnapshot.DecodeShifterGear(buttons),
                CommandedConstantForcePercent = commandedForce,
                RpmLedNormalized = rpmNormalized,
                ActualControlLoopHz = actualLoopHz,
                CombinedPedalsWarning = _combinedPedalsWarning
            };
        }

        Volatile.Write(ref _latestSnapshot, snapshot);
        TelemetryUpdated?.Invoke(snapshot);
    }

    private static double ResolveEffectiveTargetAngle(
        ControlMode mode,
        double manualTargetDeg,
        double halfRange,
        PedalCouplingMode couplingMode,
        double gasPercent)
    {
        if (mode == ControlMode.MotorPositioning && couplingMode == PedalCouplingMode.GasToTargetAngle)
        {
            return ((gasPercent / 100.0) * 2.0 - 1.0) * halfRange;
        }

        return Math.Clamp(manualTargetDeg, -halfRange, halfRange);
    }

    private static int ResolveEffectiveBumpyPercent(
        FfbEffectState effects,
        PedalCouplingMode couplingMode,
        double gasPercent)
    {
        int basePct = effects.BumpyRoadEnabled ? effects.BumpyRoadMagnitudePercent : 0;
        if (couplingMode == PedalCouplingMode.GasToBumpyVibration)
        {
            int coupledPct = (int)Math.Round(gasPercent);
            return Math.Clamp(Math.Max(basePct, coupledPct), 0, 100);
        }

        return Math.Clamp(basePct, 0, 100);
    }

    private static double ResolveRpmNormalized(RpmLedMode mode, double manualRpmPct, double gasPercent)
    {
        double pct = mode == RpmLedMode.AcceleratorCoupled ? gasPercent : manualRpmPct;
        return Math.Clamp(pct / 100.0, 0.0, 1.0);
    }

    private static double RawXToPercent(int rawX)
    {
        double pct = rawX >= 0
            ? (rawX / 32767.0) * 100.0
            : (rawX / 32768.0) * 100.0;
        return Math.Clamp(pct, -100.0, 100.0);
    }

    private static bool[] ExtractButtons(byte[]? rawButtons)
    {
        bool[] result = new bool[WheelTelemetrySnapshot.ButtonCount];
        if (rawButtons is null)
        {
            return result;
        }

        int len = Math.Min(result.Length, rawButtons.Length);
        for (int i = 0; i < len; i++)
        {
            result[i] = (rawButtons[i] & 0x80) != 0;
        }

        return result;
    }

    public void SetWindowFocused(bool focused)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = _isWindowFocused != focused;
            _isWindowFocused = focused;
        }

        if (changed)
        {
            _pidController.Reset();
            lock (_sdkLock)
            {
                if (!focused)
                {
                    StopAllHardwareEffectsUnsafe();
                }
                else if (_sdkInitialized && _wheelIndex >= 0)
                {
                    LogitechNative.LogiSetOperatingRange(_wheelIndex, _operatingRangeDegrees);
                }
            }
        }
    }

    public void SetOperatingRange(int rangeDegrees)
    {
        int normalized = Math.Clamp(rangeDegrees, 180, 900);
        double half = normalized / 2.0;

        lock (_stateLock)
        {
            _operatingRangeDegrees = normalized;
            _targetAngleDegrees = Math.Clamp(_targetAngleDegrees, -half, half);
        }

        lock (_sdkLock)
        {
            if (_sdkInitialized && _wheelIndex >= 0 && _connectionState == HardwareConnectionState.Connected)
            {
                LogitechNative.LogiSetOperatingRange(_wheelIndex, normalized);
            }
        }
    }

    public void UpdateControlParameters(
        ControlMode controlMode,
        double targetAngleDegrees,
        bool freewheelGentleDampingEnabled,
        int freewheelDampingPercent,
        FfbEffectState ffbEffects,
        PedalCouplingMode pedalCouplingMode,
        RpmLedMode rpmLedMode,
        double manualRpmPercent)
    {
        lock (_stateLock)
        {
            double halfRange = _operatingRangeDegrees / 2.0;
            bool switchedMode = _controlMode != controlMode;
            _controlMode = controlMode;
            _targetAngleDegrees = Math.Clamp(targetAngleDegrees, -halfRange, halfRange);
            _freewheelGentleDampingEnabled = freewheelGentleDampingEnabled;
            _freewheelDampingPercent = Math.Clamp(freewheelDampingPercent, 0, 100);
            _ffbEffects = ffbEffects;
            _pedalCouplingMode = pedalCouplingMode;
            _rpmLedMode = rpmLedMode;
            _manualRpmPercent = Math.Clamp(manualRpmPercent, 0.0, 100.0);

            if (switchedMode)
            {
                _pidController.Reset();
                _polarityWarningActive = false;
            }
        }
    }

    public void UpdatePidParameters(
        double kp,
        double kd,
        double deadzoneDegrees,
        int maxForcePercent)
    {
        _pidController.Kp = kp;
        _pidController.Kd = kd;
        _pidController.DeadzoneDegrees = deadzoneDegrees;
        _pidController.MaxForcePercent = maxForcePercent;
    }

    public void SetPedalAutoCalibration(bool enabled)
    {
        _pedalCalibration.AutoCalibrationEnabled = enabled;
    }

    public void ResetPedalCalibration()
    {
        _pedalCalibration.ResetAll();
    }

    public void TriggerFrontalCollision(int magnitudePercent)
    {
        int clamped = Math.Clamp(magnitudePercent, 0, 100);
        if (clamped == 0)
        {
            return;
        }

        bool isFocused;
        lock (_stateLock)
        {
            isFocused = _isWindowFocused;
        }

        if (!isFocused || !_sdkInitialized)
        {
            return;
        }

        lock (_sdkLock)
        {
            if (_wheelIndex >= 0 && _connectionState == HardwareConnectionState.Connected)
            {
                LogitechNative.LogiPlayFrontalCollisionForce(_wheelIndex, clamped);
            }
        }
    }

    public void EmergencyStop()
    {
        lock (_stateLock)
        {
            _controlMode = ControlMode.TelemetryFreewheel;
            _freewheelGentleDampingEnabled = false;
            _ffbEffects = _ffbEffects with
            {
                SpringEnabled = false,
                DamperEnabled = false,
                BumpyRoadEnabled = false,
                DirtRoadEnabled = false
            };
            if (_pedalCouplingMode == PedalCouplingMode.GasToBumpyVibration)
            {
                _pedalCouplingMode = PedalCouplingMode.None;
            }
            _polarityWarningActive = false;
            _statusMessage = "NOT-AUS ausgelöst — alle Motoren gestoppt (Freilauf aktiv)";
        }

        _pidController.Reset();
        _forceWatchActive = false;
        _polarityWatchActive = false;

        lock (_sdkLock)
        {
            StopAllHardwareEffectsUnsafe();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loopCts?.Cancel();

        if (_loopThread is not null && _loopThread.IsAlive)
        {
            _loopThread.Join(500);
        }

        EmergencyStop();

        lock (_sdkLock)
        {
            if (_sdkInitialized)
            {
                try
                {
                    LogitechNative.LogiSteeringShutdown();
                }
                catch
                {
                    // Ignorieren beim Shutdown
                }

                _sdkInitialized = false;
            }

            _wheelIndex = -1;
        }

        if (_timerPeriodActive)
        {
            try
            {
                LogitechNative.timeEndPeriod(1);
            }
            catch
            {
                // Ignorieren
            }

            _timerPeriodActive = false;
        }

        _loopCts?.Dispose();
    }
}
