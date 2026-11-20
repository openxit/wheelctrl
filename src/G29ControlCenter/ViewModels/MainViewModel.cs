using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using G29ControlCenter.Converters;
using G29ControlCenter.Hardware;
using G29ControlCenter.Localization;
using G29ControlCenter.Models;
using G29ControlCenter.Overlay;

namespace G29ControlCenter.ViewModels;

public enum HubSection
{
    Devices = 0,
    Controls = 1,
    Overlay = 2,
    Gamepad = 3
}

public sealed class DeviceProfileOption : ObservableObject
{
    private readonly Func<string> _labelSelector;

    public DeviceProfileOption(string id, Func<string> labelSelector, int operatingRange, double kp, double kd, int maxForce)
    {
        Id = id;
        _labelSelector = labelSelector;
        OperatingRange = operatingRange;
        Kp = kp;
        Kd = kd;
        MaxForce = maxForce;
    }

    public string Id { get; }
    public int OperatingRange { get; }
    public double Kp { get; }
    public double Kd { get; }
    public int MaxForce { get; }
    public string DisplayName => _labelSelector();

    public void RefreshLabel() => OnPropertyChanged(nameof(DisplayName));
}

public sealed class PedalCouplingOption : ObservableObject
{
    private readonly Func<string> _labelSelector;

    public PedalCouplingOption(PedalCouplingMode mode, Func<string> labelSelector)
    {
        Mode = mode;
        _labelSelector = labelSelector;
    }

    public PedalCouplingMode Mode { get; }
    public string DisplayName => _labelSelector();

    public void RefreshLabel() => OnPropertyChanged(nameof(DisplayName));
}

/// <summary>
/// Zentrales ViewModel des G29 Control Centers (60-Hz-UI-Synchronisation gegen den 100-Hz-Hardware-Thread).
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IWheelHardwareService _hardwareService;
    private readonly DispatcherTimer _uiTimer;
    private readonly OverlayHttpServer _overlayHttpServer;
    private readonly VirtualGamepadService _virtualGamepadService;
    private StreamOverlayWindow? _overlayWindow;
    private bool _disposed;
    private bool _isTargetInputFocused;
    private bool _isInstallingGamepadDriver;
    private string _gamepadDriverInstallStatus = string.Empty;
    private double _virtualLeftStickXNorm;
    private double _virtualLeftTriggerPercent;
    private double _virtualRightTriggerPercent;
    private string _virtualPressedButtonsSummary = "None";
    private string _gamepadLatencyText = "0.00 ms";
    private double _gamepadProcessingMs;
    private double _gamepadLoopHz = 100.0;

    // Hub-Navigation & Overlay-Status
    private HubSection _activeSection = HubSection.Devices;
    private bool _isHomeGridView = true;
    private DeviceProfileOption _selectedProfileOption;
    private bool _isDesktopOverlayOpen;
    private bool _overlayUrlCopiedVisible;
    private string _browserServerError = string.Empty;
    private OverlayBackgroundOption _selectedOverlayBackgroundOption;

    // Status-Badges
    private string _deviceBadgeText = "Device: Initializing...";
    private BadgeSeverity _deviceBadgeLevel = BadgeSeverity.Neutral;
    private string _focusBadgeText = "Focus: Active (FFB ready)";
    private BadgeSeverity _focusBadgeLevel = BadgeSeverity.Ok;
    private string _powerBadgeText = "24V Motors: Ready";
    private BadgeSeverity _powerBadgeLevel = BadgeSeverity.Neutral;
    private string _powerBadgeTooltip = "Monitors physical motor response when force is applied.";
    private string _statusDetailLine = "Ready";
    private bool _isDeviceConnected;
    private bool _combinedPedalsWarning;

    // Lenkrad-Betriebsmodus & Telemetrie
    private ControlMode _currentControlMode = ControlMode.TelemetryFreewheel;
    private bool _freewheelGentleDampingEnabled;
    private int _freewheelDampingPercent = 15;
    private int _operatingRangeDegrees = 900;
    private double _currentAngleDegrees;
    private double _currentAnglePercent;
    private int _currentRawX;
    private double _targetAngleDegrees;
    private string _targetAngleInputText = "0,0";

    // PI-D-Regler & Limiter
    private int _maxMotorForcePercent = 50;
    private double _pidKp = 1.25;
    private double _pidKd = 0.085;
    private double _pidDeadzoneDegrees = 0.5;

    // Force-Feedback-Effekt-Labor
    private bool _springEnabled;
    private int _springOffsetPercent;
    private int _springSaturationPercent = 70;
    private int _springCoefficientPercent = 60;
    private bool _damperEnabled;
    private int _damperCoefficientPercent = 40;
    private bool _bumpyRoadEnabled;
    private int _bumpyRoadMagnitudePercent = 35;
    private bool _dirtRoadEnabled;
    private int _dirtRoadMagnitudePercent = 30;
    private int _collisionMagnitudePercent = 50;

    // Pedale & Kalibrierung
    private double _gasPercent;
    private int _gasRaw = PedalAxisCalibrator.NominalReleasedRaw;
    private double _brakePercent;
    private int _brakeRaw = PedalAxisCalibrator.NominalReleasedRaw;
    private double _clutchPercent;
    private int _clutchRaw = PedalAxisCalibrator.NominalReleasedRaw;
    private bool _autoCalibratePedals = false;
    private PedalCouplingOption _selectedPedalCouplingOption;

    // RPM-LEDs
    private RpmLedMode _selectedRpmLedMode = RpmLedMode.Manual;
    private double _manualRpmPercent;
    private double _displayedRpmNormalized;

    // Shifter & D-Pad
    private string _dPadLabel = "Mitte";
    private string _shifterGear = "N";
    private bool _leftPaddlePressed;
    private bool _rightPaddlePressed;

    public MainViewModel() : this(new LogitechG29Service())
    {
    }

    public MainViewModel(IWheelHardwareService hardwareService)
    {
        _hardwareService = hardwareService ?? throw new ArgumentNullException(nameof(hardwareService));
        _overlayHttpServer = new OverlayHttpServer(_hardwareService, Overlay);
        _virtualGamepadService = new VirtualGamepadService(GamepadConfig);
        _hardwareService.TelemetryUpdated += OnHardwareTelemetryUpdated;
        _virtualGamepadService.CheckDriverStatus();

        Buttons = new ObservableCollection<ButtonItemModel>(ButtonItemModel.CreateG29DefaultMap());
        PedalCouplingOptions =
        [
            new(PedalCouplingMode.None, () => Loc.CouplingNone),
            new(PedalCouplingMode.GasToTargetAngle, () => Loc.CouplingGasToAngle),
            new(PedalCouplingMode.GasToBumpyVibration, () => Loc.CouplingGasToBumpy)
        ];
        _selectedPedalCouplingOption = PedalCouplingOptions[0];

        OverlayBackgroundOptions =
        [
            new(OverlayBackgroundMode.Transparent, () => Loc.OverlayBgTransparent),
            new(OverlayBackgroundMode.ChromaGreen, () => Loc.OverlayBgChromaGreen),
            new(OverlayBackgroundMode.ChromaBlue, () => Loc.OverlayBgChromaBlue),
            new(OverlayBackgroundMode.DarkSolid, () => Loc.OverlayBgDark)
        ];
        _selectedOverlayBackgroundOption = OverlayBackgroundOptions[0];

        ProfileOptions =
        [
            new("standard", () => Loc.ProfileStandardName, 900, 1.25, 0.085, 50),
            new("drift", () => Loc.ProfileDriftName, 540, 1.45, 0.070, 60),
            new("formula", () => Loc.ProfileFormulaName, 360, 1.65, 0.095, 55)
        ];
        _selectedProfileOption = ProfileOptions[0];

        Overlay.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlaySettings.BrowserServerEnabled))
            {
                SyncOverlayHttpServerState();
            }
        };

        Loc.PropertyChanged += (_, _) =>
        {
            foreach (PedalCouplingOption opt in PedalCouplingOptions)
            {
                opt.RefreshLabel();
            }
            foreach (OverlayBackgroundOption bgOpt in OverlayBackgroundOptions)
            {
                bgOpt.RefreshLabel();
            }
            foreach (DeviceProfileOption profOpt in ProfileOptions)
            {
                profOpt.RefreshLabel();
            }
            OnPropertyChanged(nameof(HubPrimaryDeviceSubtitle));
            OnPropertyChanged(nameof(DesktopOverlayButtonText));
            PullLatestTelemetry();
        };

        EmergencyStopCommand = new RelayCommand(ExecuteEmergencyStop);
        ReconnectHardwareCommand = new RelayCommand(() => _hardwareService.TryReconnect());
        SetQuickAngleCommand = new RelayCommand(ExecuteSetQuickAngle);
        SetOperatingRangeCommand = new RelayCommand(ExecuteSetOperatingRange);
        ApplyTargetAngleCommand = new RelayCommand(ApplyTargetAngleFromText);
        ResetPidDefaultsCommand = new RelayCommand(ResetPidDefaults);
        TriggerCollisionCommand = new RelayCommand(() => _hardwareService.TriggerFrontalCollision(_collisionMagnitudePercent));
        ResetPedalCalibrationCommand = new RelayCommand(() => _hardwareService.ResetPedalCalibration());
        ToggleDesktopOverlayCommand = new RelayCommand(ToggleDesktopOverlay);
        CopyOverlayUrlCommand = new RelayCommand(CopyOverlayUrl);
        OpenDevicesTabCommand = new RelayCommand(() => ActiveSection = HubSection.Devices);
        OpenControlsTabCommand = new RelayCommand(() => ActiveSection = HubSection.Controls);
        OpenOverlayTabCommand = new RelayCommand(() => ActiveSection = HubSection.Overlay);
        OpenGamepadTabCommand = new RelayCommand(() => ActiveSection = HubSection.Gamepad);
        InstallGamepadDriverCommand = new RelayCommand(() => _ = InstallGamepadDriverAsync());
        RecheckGamepadDriverCommand = new RelayCommand(RecheckGamepadDriver);
        ApplyGamepadDefaultPresetCommand = new RelayCommand(() => GamepadConfig.ApplyDefaultPreset());
        ClearGamepadMappingsCommand = new RelayCommand(() => GamepadConfig.ClearAllButtonMappings());

        _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _uiTimer.Tick += (_, _) => PullLatestTelemetry();
    }

    public LocalizationStrings Loc { get; } = new();
    public OverlaySettings Overlay { get; } = new();
    public VirtualGamepadMappingConfig GamepadConfig { get; } = new();
    public IReadOnlyList<VirtualAxisTarget> AxisTargetOptions { get; } = Enum.GetValues<VirtualAxisTarget>();
    public IReadOnlyList<VirtualButtonTarget> ButtonTargetOptions { get; } = Enum.GetValues<VirtualButtonTarget>();
    public ObservableCollection<ButtonItemModel> Buttons { get; }
    public IReadOnlyList<PedalCouplingOption> PedalCouplingOptions { get; }
    public IReadOnlyList<OverlayBackgroundOption> OverlayBackgroundOptions { get; }
    public IReadOnlyList<DeviceProfileOption> ProfileOptions { get; }

    public RelayCommand EmergencyStopCommand { get; }
    public RelayCommand ReconnectHardwareCommand { get; }
    public RelayCommand SetQuickAngleCommand { get; }
    public RelayCommand SetOperatingRangeCommand { get; }
    public RelayCommand ApplyTargetAngleCommand { get; }
    public RelayCommand ResetPidDefaultsCommand { get; }
    public RelayCommand TriggerCollisionCommand { get; }
    public RelayCommand ResetPedalCalibrationCommand { get; }
    public RelayCommand ToggleDesktopOverlayCommand { get; }
    public RelayCommand CopyOverlayUrlCommand { get; }
    public RelayCommand OpenDevicesTabCommand { get; }
    public RelayCommand OpenControlsTabCommand { get; }
    public RelayCommand OpenOverlayTabCommand { get; }
    public RelayCommand OpenGamepadTabCommand { get; }
    public RelayCommand InstallGamepadDriverCommand { get; }
    public RelayCommand RecheckGamepadDriverCommand { get; }
    public RelayCommand ApplyGamepadDefaultPresetCommand { get; }
    public RelayCommand ClearGamepadMappingsCommand { get; }

    public HubSection ActiveSection
    {
        get => _activeSection;
        set
        {
            if (SetProperty(ref _activeSection, value))
            {
                OnPropertyChanged(nameof(IsDevicesTabSelected));
                OnPropertyChanged(nameof(IsInsideDevice));
                OnPropertyChanged(nameof(IsControlsTabSelected));
                OnPropertyChanged(nameof(IsOverlayTabSelected));
                OnPropertyChanged(nameof(IsGamepadTabSelected));
            }
        }
    }

    public bool IsInsideDevice => _activeSection != HubSection.Devices;

    public bool IsDevicesTabSelected
    {
        get => _activeSection == HubSection.Devices;
        set
        {
            if (value)
            {
                ActiveSection = HubSection.Devices;
            }
        }
    }

    public bool IsControlsTabSelected
    {
        get => _activeSection == HubSection.Controls;
        set
        {
            if (value)
            {
                ActiveSection = HubSection.Controls;
            }
        }
    }

    public bool IsOverlayTabSelected
    {
        get => _activeSection == HubSection.Overlay;
        set
        {
            if (value)
            {
                ActiveSection = HubSection.Overlay;
            }
        }
    }

    public bool IsGamepadTabSelected
    {
        get => _activeSection == HubSection.Gamepad;
        set
        {
            if (value)
            {
                ActiveSection = HubSection.Gamepad;
            }
        }
    }

    public bool IsHomeGridView
    {
        get => _isHomeGridView;
        set
        {
            if (SetProperty(ref _isHomeGridView, value))
            {
                OnPropertyChanged(nameof(IsHomeListView));
            }
        }
    }

    public bool IsHomeListView
    {
        get => !_isHomeGridView;
        set => IsHomeGridView = !value;
    }

    public DeviceProfileOption SelectedProfileOption
    {
        get => _selectedProfileOption;
        set
        {
            if (SetProperty(ref _selectedProfileOption, value) && value is not null)
            {
                OperatingRangeDegrees = value.OperatingRange;
                PidKp = value.Kp;
                PidKd = value.Kd;
                MaxMotorForcePercent = value.MaxForce;
            }
        }
    }

    public bool IsDesktopOverlayOpen
    {
        get => _isDesktopOverlayOpen;
        private set
        {
            if (SetProperty(ref _isDesktopOverlayOpen, value))
            {
                OnPropertyChanged(nameof(DesktopOverlayButtonText));
                OnPropertyChanged(nameof(IsAnyOverlayActive));
            }
        }
    }

    public bool IsAnyOverlayActive => _isDesktopOverlayOpen || Overlay.BrowserServerEnabled;

    public string DesktopOverlayButtonText =>
        _isDesktopOverlayOpen ? Loc.OverlayCloseButton : Loc.OverlayOpenButton;

    public string HubPrimaryDeviceSubtitle =>
        _isDeviceConnected ? Loc.HubPrimaryDeviceSubConnected : Loc.HubPrimaryDeviceSubWaiting;

    public bool OverlayUrlCopiedVisible
    {
        get => _overlayUrlCopiedVisible;
        private set => SetProperty(ref _overlayUrlCopiedVisible, value);
    }

    public string BrowserServerError
    {
        get => _browserServerError;
        private set => SetProperty(ref _browserServerError, value);
    }

    public OverlayBackgroundOption SelectedOverlayBackgroundOption
    {
        get => _selectedOverlayBackgroundOption;
        set
        {
            if (SetProperty(ref _selectedOverlayBackgroundOption, value))
            {
                Overlay.BackgroundMode = value.Mode;
            }
        }
    }

    public double GasBarHeight => Math.Clamp(_gasPercent / 100.0, 0.0, 1.0) * 124.0;
    public double BrakeBarHeight => Math.Clamp(_brakePercent / 100.0, 0.0, 1.0) * 124.0;
    public double ClutchBarHeight => Math.Clamp(_clutchPercent / 100.0, 0.0, 1.0) * 124.0;

    public string DeviceBadgeText
    {
        get => _deviceBadgeText;
        private set => SetProperty(ref _deviceBadgeText, value);
    }

    public BadgeSeverity DeviceBadgeLevel
    {
        get => _deviceBadgeLevel;
        private set => SetProperty(ref _deviceBadgeLevel, value);
    }

    public string FocusBadgeText
    {
        get => _focusBadgeText;
        private set => SetProperty(ref _focusBadgeText, value);
    }

    public BadgeSeverity FocusBadgeLevel
    {
        get => _focusBadgeLevel;
        private set => SetProperty(ref _focusBadgeLevel, value);
    }

    public string PowerBadgeText
    {
        get => _powerBadgeText;
        private set => SetProperty(ref _powerBadgeText, value);
    }

    public BadgeSeverity PowerBadgeLevel
    {
        get => _powerBadgeLevel;
        private set => SetProperty(ref _powerBadgeLevel, value);
    }

    public string PowerBadgeTooltip
    {
        get => _powerBadgeTooltip;
        private set => SetProperty(ref _powerBadgeTooltip, value);
    }

    public string StatusDetailLine
    {
        get => _statusDetailLine;
        private set => SetProperty(ref _statusDetailLine, value);
    }

    public bool IsDeviceConnected
    {
        get => _isDeviceConnected;
        private set
        {
            if (SetProperty(ref _isDeviceConnected, value))
            {
                OnPropertyChanged(nameof(IsDeviceDisconnected));
            }
        }
    }

    public bool IsDeviceDisconnected => !_isDeviceConnected;

    public bool IsVirtualGamepadEnabled
    {
        get => _virtualGamepadService.IsEnabled;
        set
        {
            _virtualGamepadService.SetEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGamepadDriverReady));
            OnPropertyChanged(nameof(IsGamepadDriverMissing));
            OnPropertyChanged(nameof(GamepadDriverStatusText));
        }
    }

    public bool IsGamepadDriverReady => _virtualGamepadService.DriverState == VirtualGamepadDriverState.Ready;
    public bool IsGamepadDriverMissing => _virtualGamepadService.DriverState == VirtualGamepadDriverState.DriverMissing;

    public bool IsInstallingGamepadDriver
    {
        get => _isInstallingGamepadDriver;
        private set
        {
            if (SetProperty(ref _isInstallingGamepadDriver, value))
            {
                OnPropertyChanged(nameof(GamepadDriverStatusText));
            }
        }
    }

    public string GamepadDriverStatusText
    {
        get
        {
            if (_isInstallingGamepadDriver)
            {
                return Loc.GamepadInstallingDriver;
            }
            if (!string.IsNullOrEmpty(_gamepadDriverInstallStatus))
            {
                return _gamepadDriverInstallStatus;
            }
            return _virtualGamepadService.DriverState == VirtualGamepadDriverState.Ready
                ? Loc.GamepadDriverReady
                : Loc.GamepadDriverMissing;
        }
    }

    public double VirtualLeftStickXNorm
    {
        get => _virtualLeftStickXNorm;
        private set => SetProperty(ref _virtualLeftStickXNorm, value);
    }

    public double VirtualLeftTriggerPercent
    {
        get => _virtualLeftTriggerPercent;
        private set => SetProperty(ref _virtualLeftTriggerPercent, value);
    }

    public double VirtualRightTriggerPercent
    {
        get => _virtualRightTriggerPercent;
        private set => SetProperty(ref _virtualRightTriggerPercent, value);
    }

    public string VirtualPressedButtonsSummary
    {
        get => _virtualPressedButtonsSummary;
        private set => SetProperty(ref _virtualPressedButtonsSummary, value);
    }

    public string GamepadLatencyText
    {
        get => _gamepadLatencyText;
        private set => SetProperty(ref _gamepadLatencyText, value);
    }

    public double GamepadProcessingMs
    {
        get => _gamepadProcessingMs;
        private set => SetProperty(ref _gamepadProcessingMs, value);
    }

    public double GamepadLoopHz
    {
        get => _gamepadLoopHz;
        private set => SetProperty(ref _gamepadLoopHz, value);
    }

    public bool CombinedPedalsWarning
    {
        get => _combinedPedalsWarning;
        private set => SetProperty(ref _combinedPedalsWarning, value);
    }

    public ControlMode CurrentControlMode
    {
        get => _currentControlMode;
        set
        {
            if (SetProperty(ref _currentControlMode, value))
            {
                OnPropertyChanged(nameof(IsFreewheelMode));
                OnPropertyChanged(nameof(IsMotorPositioningMode));
                PushControlParameters();
            }
        }
    }

    public bool IsFreewheelMode
    {
        get => _currentControlMode == ControlMode.TelemetryFreewheel;
        set
        {
            if (value)
            {
                CurrentControlMode = ControlMode.TelemetryFreewheel;
            }
        }
    }

    public bool IsMotorPositioningMode
    {
        get => _currentControlMode == ControlMode.MotorPositioning;
        set
        {
            if (value)
            {
                CurrentControlMode = ControlMode.MotorPositioning;
            }
        }
    }

    public bool FreewheelGentleDampingEnabled
    {
        get => _freewheelGentleDampingEnabled;
        set
        {
            if (SetProperty(ref _freewheelGentleDampingEnabled, value))
            {
                PushControlParameters();
            }
        }
    }

    public int FreewheelDampingPercent
    {
        get => _freewheelDampingPercent;
        set
        {
            int clamped = Math.Clamp(value, 0, 50);
            if (SetProperty(ref _freewheelDampingPercent, clamped))
            {
                PushControlParameters();
            }
        }
    }

    public int OperatingRangeDegrees
    {
        get => _operatingRangeDegrees;
        set
        {
            int clamped = Math.Clamp(value, 180, 900);
            if (SetProperty(ref _operatingRangeDegrees, clamped))
            {
                OnPropertyChanged(nameof(HalfOperatingRangeDegrees));
                OnPropertyChanged(nameof(MinOperatingAngleDegrees));
                OnPropertyChanged(nameof(MaxOperatingAngleDegrees));
                _hardwareService.SetOperatingRange(clamped);

                double half = clamped / 2.0;
                if (Math.Abs(_targetAngleDegrees) > half)
                {
                    TargetAngleDegrees = Math.Clamp(_targetAngleDegrees, -half, half);
                }
                else
                {
                    PushControlParameters();
                }
            }
        }
    }

    public double HalfOperatingRangeDegrees => _operatingRangeDegrees / 2.0;
    public double MinOperatingAngleDegrees => -HalfOperatingRangeDegrees;
    public double MaxOperatingAngleDegrees => HalfOperatingRangeDegrees;

    public double CurrentAngleDegrees
    {
        get => _currentAngleDegrees;
        private set
        {
            if (SetProperty(ref _currentAngleDegrees, value))
            {
                OnPropertyChanged(nameof(CurrentAngleFormatted));
            }
        }
    }

    public string CurrentAngleFormatted =>
        string.Format(CultureInfo.GetCultureInfo("de-DE"), "{0:+0.0;-0.0;0.0}°", _currentAngleDegrees);

    public double CurrentAnglePercent
    {
        get => _currentAnglePercent;
        private set
        {
            if (SetProperty(ref _currentAnglePercent, value))
            {
                OnPropertyChanged(nameof(CurrentPercentFormatted));
            }
        }
    }

    public string CurrentPercentFormatted =>
        string.Format(CultureInfo.GetCultureInfo("de-DE"), "{0:+0.0;-0.0;0.0} %", _currentAnglePercent);

    public int CurrentRawX
    {
        get => _currentRawX;
        private set => SetProperty(ref _currentRawX, value);
    }

    public double TargetAngleDegrees
    {
        get => _targetAngleDegrees;
        set
        {
            double half = HalfOperatingRangeDegrees;
            double clamped = Math.Round(Math.Clamp(value, -half, half), 1);
            if (SetProperty(ref _targetAngleDegrees, clamped))
            {
                if (!_isTargetInputFocused)
                {
                    SetProperty(
                        ref _targetAngleInputText,
                        clamped.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")),
                        nameof(TargetAngleInputText));
                }
                PushControlParameters();
            }
        }
    }

    public string TargetAngleInputText
    {
        get => _targetAngleInputText;
        set => SetProperty(ref _targetAngleInputText, value);
    }

    public int MaxMotorForcePercent
    {
        get => _maxMotorForcePercent;
        set
        {
            int clamped = Math.Clamp(value, 10, 100);
            if (SetProperty(ref _maxMotorForcePercent, clamped))
            {
                PushPidParameters();
            }
        }
    }

    public double PidKp
    {
        get => _pidKp;
        set
        {
            double clamped = Math.Round(Math.Clamp(value, 0.05, 5.0), 2);
            if (SetProperty(ref _pidKp, clamped))
            {
                PushPidParameters();
            }
        }
    }

    public double PidKd
    {
        get => _pidKd;
        set
        {
            double clamped = Math.Round(Math.Clamp(value, 0.0, 0.5), 3);
            if (SetProperty(ref _pidKd, clamped))
            {
                PushPidParameters();
            }
        }
    }

    public double PidDeadzoneDegrees
    {
        get => _pidDeadzoneDegrees;
        set
        {
            double clamped = Math.Round(Math.Clamp(value, 0.0, 3.0), 1);
            if (SetProperty(ref _pidDeadzoneDegrees, clamped))
            {
                PushPidParameters();
            }
        }
    }

    public bool SpringEnabled
    {
        get => _springEnabled;
        set
        {
            if (SetProperty(ref _springEnabled, value))
            {
                PushControlParameters();
            }
        }
    }

    public int SpringOffsetPercent
    {
        get => _springOffsetPercent;
        set
        {
            if (SetProperty(ref _springOffsetPercent, Math.Clamp(value, -100, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public int SpringSaturationPercent
    {
        get => _springSaturationPercent;
        set
        {
            if (SetProperty(ref _springSaturationPercent, Math.Clamp(value, 0, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public int SpringCoefficientPercent
    {
        get => _springCoefficientPercent;
        set
        {
            if (SetProperty(ref _springCoefficientPercent, Math.Clamp(value, 0, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public bool DamperEnabled
    {
        get => _damperEnabled;
        set
        {
            if (SetProperty(ref _damperEnabled, value))
            {
                PushControlParameters();
            }
        }
    }

    public int DamperCoefficientPercent
    {
        get => _damperCoefficientPercent;
        set
        {
            if (SetProperty(ref _damperCoefficientPercent, Math.Clamp(value, 0, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public bool BumpyRoadEnabled
    {
        get => _bumpyRoadEnabled;
        set
        {
            if (SetProperty(ref _bumpyRoadEnabled, value))
            {
                PushControlParameters();
            }
        }
    }

    public int BumpyRoadMagnitudePercent
    {
        get => _bumpyRoadMagnitudePercent;
        set
        {
            if (SetProperty(ref _bumpyRoadMagnitudePercent, Math.Clamp(value, 0, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public bool DirtRoadEnabled
    {
        get => _dirtRoadEnabled;
        set
        {
            if (SetProperty(ref _dirtRoadEnabled, value))
            {
                PushControlParameters();
            }
        }
    }

    public int DirtRoadMagnitudePercent
    {
        get => _dirtRoadMagnitudePercent;
        set
        {
            if (SetProperty(ref _dirtRoadMagnitudePercent, Math.Clamp(value, 0, 100)))
            {
                PushControlParameters();
            }
        }
    }

    public int CollisionMagnitudePercent
    {
        get => _collisionMagnitudePercent;
        set => SetProperty(ref _collisionMagnitudePercent, Math.Clamp(value, 10, 100));
    }

    public double GasPercent
    {
        get => _gasPercent;
        private set
        {
            if (SetProperty(ref _gasPercent, value))
            {
                OnPropertyChanged(nameof(GasBarHeight));
            }
        }
    }

    public int GasRaw
    {
        get => _gasRaw;
        private set => SetProperty(ref _gasRaw, value);
    }

    public double BrakePercent
    {
        get => _brakePercent;
        private set
        {
            if (SetProperty(ref _brakePercent, value))
            {
                OnPropertyChanged(nameof(BrakeBarHeight));
            }
        }
    }

    public int BrakeRaw
    {
        get => _brakeRaw;
        private set => SetProperty(ref _brakeRaw, value);
    }

    public double ClutchPercent
    {
        get => _clutchPercent;
        private set
        {
            if (SetProperty(ref _clutchPercent, value))
            {
                OnPropertyChanged(nameof(ClutchBarHeight));
            }
        }
    }

    public int ClutchRaw
    {
        get => _clutchRaw;
        private set => SetProperty(ref _clutchRaw, value);
    }

    public bool AutoCalibratePedals
    {
        get => _autoCalibratePedals;
        set
        {
            if (SetProperty(ref _autoCalibratePedals, value))
            {
                _hardwareService.SetPedalAutoCalibration(value);
            }
        }
    }

    public PedalCouplingOption SelectedPedalCouplingOption
    {
        get => _selectedPedalCouplingOption;
        set
        {
            if (SetProperty(ref _selectedPedalCouplingOption, value))
            {
                PushControlParameters();
            }
        }
    }

    public RpmLedMode SelectedRpmLedMode
    {
        get => _selectedRpmLedMode;
        set
        {
            if (SetProperty(ref _selectedRpmLedMode, value))
            {
                OnPropertyChanged(nameof(IsRpmManualMode));
                OnPropertyChanged(nameof(IsRpmGasCoupledMode));
                PushControlParameters();
            }
        }
    }

    public bool IsRpmManualMode
    {
        get => _selectedRpmLedMode == RpmLedMode.Manual;
        set
        {
            if (value)
            {
                SelectedRpmLedMode = RpmLedMode.Manual;
            }
        }
    }

    public bool IsRpmGasCoupledMode
    {
        get => _selectedRpmLedMode == RpmLedMode.AcceleratorCoupled;
        set
        {
            if (value)
            {
                SelectedRpmLedMode = RpmLedMode.AcceleratorCoupled;
            }
        }
    }

    public double ManualRpmPercent
    {
        get => _manualRpmPercent;
        set
        {
            if (SetProperty(ref _manualRpmPercent, Math.Clamp(value, 0.0, 100.0)))
            {
                PushControlParameters();
            }
        }
    }

    public double DisplayedRpmNormalized
    {
        get => _displayedRpmNormalized;
        private set => SetProperty(ref _displayedRpmNormalized, value);
    }

    public string DPadLabel
    {
        get => _dPadLabel;
        private set => SetProperty(ref _dPadLabel, value);
    }

    public string ShifterGear
    {
        get => _shifterGear;
        private set => SetProperty(ref _shifterGear, value);
    }

    public bool LeftPaddlePressed
    {
        get => _leftPaddlePressed;
        private set => SetProperty(ref _leftPaddlePressed, value);
    }

    public bool RightPaddlePressed
    {
        get => _rightPaddlePressed;
        private set => SetProperty(ref _rightPaddlePressed, value);
    }

    public void InitializeHardware(nint windowHandle)
    {
        _hardwareService.Initialize(windowHandle);
        PushPidParameters();
        PushControlParameters();
        PullLatestTelemetry();
        _uiTimer.Start();
    }

    public void SetWindowFocused(bool focused)
    {
        _hardwareService.SetWindowFocused(focused);
    }

    public void SetTargetInputFocus(bool hasFocus)
    {
        _isTargetInputFocused = hasFocus;
    }

    public void HandleWheelDraggedAngle(double newAngleDeg)
    {
        if (IsMotorPositioningMode)
        {
            TargetAngleDegrees = newAngleDeg;
        }
    }

    public void Shutdown()
    {
        if (_disposed)
        {
            return;
        }

        _uiTimer.Stop();
        _hardwareService.TelemetryUpdated -= OnHardwareTelemetryUpdated;
        if (_overlayWindow is not null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }
        _overlayHttpServer.Dispose();
        _virtualGamepadService.Dispose();
        _hardwareService.EmergencyStop();
        _hardwareService.Dispose();
        _disposed = true;
    }

    private void OnHardwareTelemetryUpdated(WheelTelemetrySnapshot snapshot)
    {
        _virtualGamepadService.ProcessSnapshot(snapshot);
    }

    private void RecheckGamepadDriver()
    {
        _gamepadDriverInstallStatus = string.Empty;
        _virtualGamepadService.CheckDriverStatus();
        OnPropertyChanged(nameof(IsGamepadDriverReady));
        OnPropertyChanged(nameof(IsGamepadDriverMissing));
        OnPropertyChanged(nameof(GamepadDriverStatusText));
    }

    private async Task InstallGamepadDriverAsync()
    {
        if (IsInstallingGamepadDriver)
        {
            return;
        }

        IsInstallingGamepadDriver = true;
        _gamepadDriverInstallStatus = string.Empty;

        (bool success, string message) = await _virtualGamepadService.InstallViGEmBusDriverAsync();
        _gamepadDriverInstallStatus = success ? string.Empty : message;
        IsInstallingGamepadDriver = false;

        OnPropertyChanged(nameof(IsGamepadDriverReady));
        OnPropertyChanged(nameof(IsGamepadDriverMissing));
        OnPropertyChanged(nameof(GamepadDriverStatusText));
    }

    private void ToggleDesktopOverlay()
    {
        if (_overlayWindow is not null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
            IsDesktopOverlayOpen = false;
            return;
        }

        var win = new StreamOverlayWindow(this);
        win.Closed += (_, _) =>
        {
            _overlayWindow = null;
            IsDesktopOverlayOpen = false;
        };
        _overlayWindow = win;
        win.Show();
        IsDesktopOverlayOpen = true;
    }

    private void CopyOverlayUrl()
    {
        try
        {
            Clipboard.SetText(Overlay.BrowserSourceUrl);
            OverlayUrlCopiedVisible = true;
        }
        catch
        {
            // Clipboard-Sperre ignorieren
        }
    }

    private void SyncOverlayHttpServerState()
    {
        BrowserServerError = string.Empty;
        if (Overlay.BrowserServerEnabled)
        {
            try
            {
                _overlayHttpServer.Start();
            }
            catch (Exception ex)
            {
                BrowserServerError = ex.Message;
                Overlay.BrowserServerEnabled = false;
            }
        }
        else
        {
            _overlayHttpServer.Stop();
        }

        OnPropertyChanged(nameof(IsAnyOverlayActive));
    }

    private void PullLatestTelemetry()
    {
        WheelTelemetrySnapshot s = _hardwareService.LatestSnapshot;

        bool wasConnected = IsDeviceConnected;
        IsDeviceConnected = s.ConnectionState == HardwareConnectionState.Connected;
        if (wasConnected != IsDeviceConnected)
        {
            OnPropertyChanged(nameof(HubPrimaryDeviceSubtitle));
        }
        CombinedPedalsWarning = s.CombinedPedalsWarning;

        switch (s.ConnectionState)
        {
            case HardwareConnectionState.Connected:
                DeviceBadgeText = $"{Loc.DeviceConnectedPrefix}: {s.DeviceName} (#{s.ControllerIndex})";
                DeviceBadgeLevel = BadgeSeverity.Ok;
                break;
            case HardwareConnectionState.WaitingForDevice:
                DeviceBadgeText = Loc.DeviceWaiting;
                DeviceBadgeLevel = BadgeSeverity.Warning;
                break;
            case HardwareConnectionState.DriverUnavailable:
                DeviceBadgeText = Loc.DeviceDriverMissing;
                DeviceBadgeLevel = BadgeSeverity.Error;
                break;
            default:
                DeviceBadgeText = Loc.DeviceInitializing;
                DeviceBadgeLevel = BadgeSeverity.Neutral;
                break;
        }

        if (s.IsWindowFocused)
        {
            FocusBadgeText = Loc.FocusActive;
            FocusBadgeLevel = BadgeSeverity.Ok;
        }
        else
        {
            FocusBadgeText = Loc.FocusPaused;
            FocusBadgeLevel = BadgeSeverity.Warning;
        }

        switch (s.MotorPowerState)
        {
            case MotorPowerState.Verified24V:
                PowerBadgeText = Loc.PowerVerified;
                PowerBadgeLevel = BadgeSeverity.Ok;
                break;
            case MotorPowerState.NoResponseCheck24V:
                PowerBadgeText = Loc.PowerNoResponse;
                PowerBadgeLevel = BadgeSeverity.Error;
                break;
            case MotorPowerState.UnknownReady:
                PowerBadgeText = Loc.PowerReady;
                PowerBadgeLevel = BadgeSeverity.Neutral;
                break;
            default:
                PowerBadgeText = Loc.PowerDisconnected;
                PowerBadgeLevel = BadgeSeverity.Neutral;
                break;
        }

        PowerBadgeTooltip = s.MotorStatusHint;
        StatusDetailLine = s.StatusMessage;

        CurrentAngleDegrees = s.WheelAngleDegrees;
        CurrentAnglePercent = s.WheelPercent;
        CurrentRawX = s.WheelRawX;

        // Bei Gaspedal-Kopplung wird der wirksame Zielwinkel im Slider/Lenkrad live sichtbar
        if (IsMotorPositioningMode && _selectedPedalCouplingOption.Mode == PedalCouplingMode.GasToTargetAngle)
        {
            SetProperty(ref _targetAngleDegrees, Math.Round(s.TargetAngleDegrees, 1), nameof(TargetAngleDegrees));
            if (!_isTargetInputFocused)
            {
                SetProperty(
                    ref _targetAngleInputText,
                    _targetAngleDegrees.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")),
                    nameof(TargetAngleInputText));
            }
        }

        GasPercent = s.GasPercent;
        GasRaw = s.GasRaw;
        BrakePercent = s.BrakePercent;
        BrakeRaw = s.BrakeRaw;
        ClutchPercent = s.ClutchPercent;
        ClutchRaw = s.ClutchRaw;

        DisplayedRpmNormalized = s.RpmLedNormalized;
        DPadLabel = s.DPadLabel;
        ShifterGear = s.ShifterGear;

        int btnCount = Math.Min(Buttons.Count, s.Buttons.Length);
        for (int i = 0; i < btnCount; i++)
        {
            Buttons[i].IsPressed = s.Buttons[i];
        }

        RightPaddlePressed = s.Buttons.Length > 4 && s.Buttons[4];
        LeftPaddlePressed = s.Buttons.Length > 5 && s.Buttons[5];

        // Live Virtual Gamepad Preview & Latency
        VirtualXbox360Report gpReport = VirtualGamepadReportMapper.Map(s, GamepadConfig);
        VirtualLeftStickXNorm = Math.Round((gpReport.LeftStickX / 32767.0) * 100.0, 1);
        VirtualLeftTriggerPercent = Math.Round((gpReport.LeftTrigger / 255.0) * 100.0, 1);
        VirtualRightTriggerPercent = Math.Round((gpReport.RightTrigger / 255.0) * 100.0, 1);
        VirtualPressedButtonsSummary = FormatPressedXboxButtons(gpReport.Buttons);

        for (int i = 0; i < GamepadConfig.ButtonMappings.Count && i < s.Buttons.Length; i++)
        {
            GamepadConfig.ButtonMappings[i].IsPressed = s.Buttons[i];
        }

        double hz = s.ActualControlLoopHz > 1.0 ? s.ActualControlLoopHz : 100.0;
        double procMs = _virtualGamepadService.IsEnabled
            ? _virtualGamepadService.AverageTranslateLatencyMs
            : 0.05;
        double avgPollMs = (1000.0 / hz) * 0.5;
        GamepadProcessingMs = procMs;
        GamepadLoopHz = hz;
        GamepadLatencyText = _virtualGamepadService.IsEnabled
            ? $"{procMs:0.00} ms translate • ~{avgPollMs + procMs:0.1} ms avg @ {hz:0} Hz"
            : $"~{avgPollMs:0.1} ms avg @ {hz:0} Hz (standby)";
    }

    private static string FormatPressedXboxButtons(VirtualXboxButtonMask mask)
    {
        if (mask == VirtualXboxButtonMask.None)
        {
            return "—";
        }

        var parts = new List<string>(8);
        if ((mask & VirtualXboxButtonMask.A) != 0) parts.Add("A");
        if ((mask & VirtualXboxButtonMask.B) != 0) parts.Add("B");
        if ((mask & VirtualXboxButtonMask.X) != 0) parts.Add("X");
        if ((mask & VirtualXboxButtonMask.Y) != 0) parts.Add("Y");
        if ((mask & VirtualXboxButtonMask.LeftShoulder) != 0) parts.Add("LB");
        if ((mask & VirtualXboxButtonMask.RightShoulder) != 0) parts.Add("RB");
        if ((mask & VirtualXboxButtonMask.Back) != 0) parts.Add("Back");
        if ((mask & VirtualXboxButtonMask.Start) != 0) parts.Add("Start");
        if ((mask & VirtualXboxButtonMask.LeftThumb) != 0) parts.Add("LS");
        if ((mask & VirtualXboxButtonMask.RightThumb) != 0) parts.Add("RS");
        if ((mask & VirtualXboxButtonMask.Up) != 0) parts.Add("D↑");
        if ((mask & VirtualXboxButtonMask.Down) != 0) parts.Add("D↓");
        if ((mask & VirtualXboxButtonMask.Left) != 0) parts.Add("D←");
        if ((mask & VirtualXboxButtonMask.Right) != 0) parts.Add("D→");
        return string.Join(" • ", parts);
    }

    private void ExecuteEmergencyStop()
    {
        _hardwareService.EmergencyStop();

        _currentControlMode = ControlMode.TelemetryFreewheel;
        OnPropertyChanged(nameof(CurrentControlMode));
        OnPropertyChanged(nameof(IsFreewheelMode));
        OnPropertyChanged(nameof(IsMotorPositioningMode));

        _freewheelGentleDampingEnabled = false;
        OnPropertyChanged(nameof(FreewheelGentleDampingEnabled));

        _springEnabled = false;
        _damperEnabled = false;
        _bumpyRoadEnabled = false;
        _dirtRoadEnabled = false;
        OnPropertyChanged(nameof(SpringEnabled));
        OnPropertyChanged(nameof(DamperEnabled));
        OnPropertyChanged(nameof(BumpyRoadEnabled));
        OnPropertyChanged(nameof(DirtRoadEnabled));

        if (_selectedPedalCouplingOption.Mode == PedalCouplingMode.GasToBumpyVibration)
        {
            _selectedPedalCouplingOption = PedalCouplingOptions[0];
            OnPropertyChanged(nameof(SelectedPedalCouplingOption));
        }

        PullLatestTelemetry();
    }

    private void ExecuteSetQuickAngle(object? parameter)
    {
        if (parameter is null)
        {
            return;
        }

        if (double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
        {
            if (!IsMotorPositioningMode)
            {
                CurrentControlMode = ControlMode.MotorPositioning;
            }
            TargetAngleDegrees = angle;
        }
    }

    private void ExecuteSetOperatingRange(object? parameter)
    {
        if (parameter is not null && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int range))
        {
            OperatingRangeDegrees = range;
        }
    }

    private void ApplyTargetAngleFromText()
    {
        string normalized = (_targetAngleInputText ?? "0").Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            _isTargetInputFocused = false;
            if (!IsMotorPositioningMode)
            {
                CurrentControlMode = ControlMode.MotorPositioning;
            }
            TargetAngleDegrees = parsed;
        }
    }

    private void ResetPidDefaults()
    {
        _maxMotorForcePercent = 50;
        _pidKp = 1.25;
        _pidKd = 0.085;
        _pidDeadzoneDegrees = 0.5;

        OnPropertyChanged(nameof(MaxMotorForcePercent));
        OnPropertyChanged(nameof(PidKp));
        OnPropertyChanged(nameof(PidKd));
        OnPropertyChanged(nameof(PidDeadzoneDegrees));

        PushPidParameters();
    }

    private void PushControlParameters()
    {
        var ffb = new FfbEffectState
        {
            SpringEnabled = _springEnabled,
            SpringOffsetPercent = _springOffsetPercent,
            SpringSaturationPercent = _springSaturationPercent,
            SpringCoefficientPercent = _springCoefficientPercent,
            DamperEnabled = _damperEnabled,
            DamperCoefficientPercent = _damperCoefficientPercent,
            BumpyRoadEnabled = _bumpyRoadEnabled,
            BumpyRoadMagnitudePercent = _bumpyRoadMagnitudePercent,
            DirtRoadEnabled = _dirtRoadEnabled,
            DirtRoadMagnitudePercent = _dirtRoadMagnitudePercent,
            CollisionMagnitudePercent = _collisionMagnitudePercent
        };

        _hardwareService.UpdateControlParameters(
            _currentControlMode,
            _targetAngleDegrees,
            _freewheelGentleDampingEnabled,
            _freewheelDampingPercent,
            ffb,
            _selectedPedalCouplingOption.Mode,
            _selectedRpmLedMode,
            _manualRpmPercent);
    }

    private void PushPidParameters()
    {
        _hardwareService.UpdatePidParameters(
            _pidKp,
            _pidKd,
            _pidDeadzoneDegrees,
            _maxMotorForcePercent);
    }

    public void Dispose()
    {
        Shutdown();
    }
}
