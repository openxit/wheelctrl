using System.Collections.ObjectModel;
using G29ControlCenter.Hardware;
using G29ControlCenter.ViewModels;

namespace G29ControlCenter.Models;

public enum VirtualGamepadType
{
    XInput = 0,
    DualShock4 = 1
}

public enum VirtualGamepadDriverState
{
    NotChecked = 0,
    Ready = 1,
    DriverMissing = 2,
    Error = 3
}

public enum VirtualAxisTarget
{
    Disabled = 0,
    RightTrigger = 1,
    LeftTrigger = 2,
    LeftStickX = 3,
    LeftStickY = 4,
    RightStickX = 5,
    RightStickY = 6
}

public enum VirtualButtonTarget
{
    None = 0,
    A = 1,
    B = 2,
    X = 3,
    Y = 4,
    LeftShoulder = 5,
    RightShoulder = 6,
    Back = 7,
    Start = 8,
    LeftThumb = 9,
    RightThumb = 10,
    DPadUp = 11,
    DPadDown = 12,
    DPadLeft = 13,
    DPadRight = 14
}

[Flags]
public enum VirtualXboxButtonMask : ushort
{
    None = 0,
    Up = 0x0001,
    Down = 0x0002,
    Left = 0x0004,
    Right = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public readonly record struct VirtualXbox360Report(
    short LeftStickX,
    short LeftStickY,
    short RightStickX,
    short RightStickY,
    byte LeftTrigger,
    byte RightTrigger,
    VirtualXboxButtonMask Buttons);

public sealed class GamepadButtonMappingItem : ObservableObject
{
    private VirtualButtonTarget _targetButton;
    private bool _isPressed;

    public GamepadButtonMappingItem(int wheelButtonIndex, string wheelButtonLabel, string wheelCategory, VirtualButtonTarget defaultTarget)
    {
        WheelButtonIndex = wheelButtonIndex;
        WheelButtonLabel = wheelButtonLabel;
        WheelCategory = wheelCategory;
        _targetButton = defaultTarget;
    }

    public int WheelButtonIndex { get; }
    public string WheelButtonLabel { get; }
    public string WheelCategory { get; }

    public VirtualButtonTarget TargetButton
    {
        get => _targetButton;
        set => SetProperty(ref _targetButton, value);
    }

    public bool IsPressed
    {
        get => _isPressed;
        set => SetProperty(ref _isPressed, value);
    }
}

/// <summary>
/// Konfiguration für die Live-Übersetzung von G29-Eingaben auf ein virtuelles Xbox-360-Gamepad.
/// </summary>
public sealed class VirtualGamepadMappingConfig : ObservableObject
{
    private VirtualGamepadType _controllerType = VirtualGamepadType.XInput;
    private int _steeringLockDegrees = 270;
    private double _steeringDeadzonePercent = 1.5;
    private double _steeringLinearity = 1.0;
    private bool _invertSteering;
    private VirtualAxisTarget _gasAxisTarget = VirtualAxisTarget.RightTrigger;
    private VirtualAxisTarget _brakeAxisTarget = VirtualAxisTarget.LeftTrigger;
    private VirtualAxisTarget _clutchAxisTarget = VirtualAxisTarget.Disabled;
    private bool _mapDPadToXboxDPad = true;

    public VirtualGamepadMappingConfig()
    {
        IReadOnlyList<ButtonItemModel> defaultButtons = ButtonItemModel.CreateG29DefaultMap();
        var items = new ObservableCollection<GamepadButtonMappingItem>();
        foreach (ButtonItemModel btn in defaultButtons)
        {
            items.Add(new GamepadButtonMappingItem(
                btn.Index,
                btn.Label,
                btn.Category,
                GetDefaultTargetForWheelButton(btn.Index)));
        }

        ButtonMappings = items;
    }

    public ObservableCollection<GamepadButtonMappingItem> ButtonMappings { get; }

    public VirtualGamepadType ControllerType
    {
        get => _controllerType;
        set => SetProperty(ref _controllerType, value);
    }

    public int SteeringLockDegrees
    {
        get => _steeringLockDegrees;
        set => SetProperty(ref _steeringLockDegrees, Math.Clamp(value, 90, 900));
    }

    public double SteeringDeadzonePercent
    {
        get => _steeringDeadzonePercent;
        set => SetProperty(ref _steeringDeadzonePercent, Math.Clamp(value, 0.0, 20.0));
    }

    public double SteeringLinearity
    {
        get => _steeringLinearity;
        set => SetProperty(ref _steeringLinearity, Math.Clamp(value, 0.5, 2.5));
    }

    public bool InvertSteering
    {
        get => _invertSteering;
        set => SetProperty(ref _invertSteering, value);
    }

    public VirtualAxisTarget GasAxisTarget
    {
        get => _gasAxisTarget;
        set => SetProperty(ref _gasAxisTarget, value);
    }

    public VirtualAxisTarget BrakeAxisTarget
    {
        get => _brakeAxisTarget;
        set => SetProperty(ref _brakeAxisTarget, value);
    }

    public VirtualAxisTarget ClutchAxisTarget
    {
        get => _clutchAxisTarget;
        set => SetProperty(ref _clutchAxisTarget, value);
    }

    public bool MapDPadToXboxDPad
    {
        get => _mapDPadToXboxDPad;
        set => SetProperty(ref _mapDPadToXboxDPad, value);
    }

    public void ApplyDefaultPreset()
    {
        SteeringLockDegrees = 270;
        SteeringDeadzonePercent = 1.5;
        SteeringLinearity = 1.0;
        InvertSteering = false;
        GasAxisTarget = VirtualAxisTarget.RightTrigger;
        BrakeAxisTarget = VirtualAxisTarget.LeftTrigger;
        ClutchAxisTarget = VirtualAxisTarget.Disabled;
        MapDPadToXboxDPad = true;

        foreach (GamepadButtonMappingItem item in ButtonMappings)
        {
            item.TargetButton = GetDefaultTargetForWheelButton(item.WheelButtonIndex);
        }
    }

    public void ClearAllButtonMappings()
    {
        foreach (GamepadButtonMappingItem item in ButtonMappings)
        {
            item.TargetButton = VirtualButtonTarget.None;
        }
    }

    public static VirtualButtonTarget GetDefaultTargetForWheelButton(int index) => index switch
    {
        0 => VirtualButtonTarget.A,             // ✕ -> A
        1 => VirtualButtonTarget.X,             // □ -> X
        2 => VirtualButtonTarget.B,             // ○ -> B
        3 => VirtualButtonTarget.Y,             // △ -> Y
        4 => VirtualButtonTarget.RightShoulder, // Wippe R -> RB
        5 => VirtualButtonTarget.LeftShoulder,  // Wippe L -> LB
        6 => VirtualButtonTarget.RightShoulder, // R2 -> RB
        7 => VirtualButtonTarget.LeftShoulder,  // L2 -> LB
        8 => VirtualButtonTarget.Back,          // Share -> Back
        9 => VirtualButtonTarget.Start,         // Opt -> Start
        10 => VirtualButtonTarget.RightThumb,   // R3 -> RS
        11 => VirtualButtonTarget.LeftThumb,    // L3 -> LS
        _ => VirtualButtonTarget.None
    };
}

/// <summary>
/// Zuweisungsfreie, deterministische Echtzeit-Übersetzung von WheelTelemetrySnapshot zu Xbox-360-Report.
/// </summary>
public static class VirtualGamepadReportMapper
{
    public static VirtualXbox360Report Map(WheelTelemetrySnapshot snapshot, VirtualGamepadMappingConfig config)
    {
        short lsX = MapSteeringToShort(
            snapshot.WheelAngleDegrees,
            config.SteeringLockDegrees,
            config.SteeringDeadzonePercent,
            config.SteeringLinearity,
            config.InvertSteering);

        short lsY = 0;
        short rsX = 0;
        short rsY = 0;
        byte lt = 0;
        byte rt = 0;

        ApplyPedalAxis(config.GasAxisTarget, snapshot.GasPercent, ref lsX, ref lsY, ref rsX, ref rsY, ref lt, ref rt);
        ApplyPedalAxis(config.BrakeAxisTarget, snapshot.BrakePercent, ref lsX, ref lsY, ref rsX, ref rsY, ref lt, ref rt);
        ApplyPedalAxis(config.ClutchAxisTarget, snapshot.ClutchPercent, ref lsX, ref lsY, ref rsX, ref rsY, ref lt, ref rt);

        VirtualXboxButtonMask mask = VirtualXboxButtonMask.None;

        if (config.MapDPadToXboxDPad)
        {
            mask |= MapPovToDPad(snapshot.PovRaw);
        }

        bool[] wheelButtons = snapshot.Buttons;
        for (int i = 0; i < config.ButtonMappings.Count && i < wheelButtons.Length; i++)
        {
            if (wheelButtons[i])
            {
                mask |= ToXboxMask(config.ButtonMappings[i].TargetButton);
            }
        }

        return new VirtualXbox360Report(lsX, lsY, rsX, rsY, lt, rt, mask);
    }

    public static short MapSteeringToShort(
        double wheelAngleDeg,
        int lockDegrees,
        double deadzonePercent,
        double linearity,
        bool invert)
    {
        double halfLock = Math.Max(45.0, lockDegrees / 2.0);
        double normalized = Math.Clamp(wheelAngleDeg / halfLock, -1.0, 1.0);
        if (invert)
        {
            normalized = -normalized;
        }

        double deadzone = Math.Clamp(deadzonePercent / 100.0, 0.0, 0.45);
        double absNorm = Math.Abs(normalized);
        if (absNorm <= deadzone)
        {
            return 0;
        }

        double rescaled = (absNorm - deadzone) / (1.0 - deadzone);
        double curved = Math.Pow(Math.Clamp(rescaled, 0.0, 1.0), Math.Clamp(linearity, 0.5, 2.5));
        double signed = Math.Sign(normalized) * curved;

        return signed >= 0
            ? (short)Math.Round(signed * short.MaxValue)
            : (short)Math.Round(signed * -short.MinValue);
    }

    private static void ApplyPedalAxis(
        VirtualAxisTarget target,
        double pedalPercent,
        ref short lsX,
        ref short lsY,
        ref short rsX,
        ref short rsY,
        ref byte lt,
        ref byte rt)
    {
        if (target == VirtualAxisTarget.Disabled)
        {
            return;
        }

        double clamped = Math.Clamp(pedalPercent / 100.0, 0.0, 1.0);
        switch (target)
        {
            case VirtualAxisTarget.RightTrigger:
                byte rVal = (byte)Math.Round(clamped * 255.0);
                if (rVal > rt) rt = rVal;
                break;
            case VirtualAxisTarget.LeftTrigger:
                byte lVal = (byte)Math.Round(clamped * 255.0);
                if (lVal > lt) lt = lVal;
                break;
            case VirtualAxisTarget.LeftStickX:
                lsX = (short)Math.Round(clamped * short.MaxValue);
                break;
            case VirtualAxisTarget.LeftStickY:
                lsY = (short)Math.Round(clamped * short.MaxValue);
                break;
            case VirtualAxisTarget.RightStickX:
                rsX = (short)Math.Round(clamped * short.MaxValue);
                break;
            case VirtualAxisTarget.RightStickY:
                rsY = (short)Math.Round(clamped * short.MaxValue);
                break;
        }
    }

    private static VirtualXboxButtonMask MapPovToDPad(uint povRaw)
    {
        if (povRaw == uint.MaxValue || povRaw == 65535)
        {
            return VirtualXboxButtonMask.None;
        }

        return povRaw switch
        {
            0 => VirtualXboxButtonMask.Up,
            4500 => VirtualXboxButtonMask.Up | VirtualXboxButtonMask.Right,
            9000 => VirtualXboxButtonMask.Right,
            13500 => VirtualXboxButtonMask.Down | VirtualXboxButtonMask.Right,
            18000 => VirtualXboxButtonMask.Down,
            22500 => VirtualXboxButtonMask.Down | VirtualXboxButtonMask.Left,
            27000 => VirtualXboxButtonMask.Left,
            31500 => VirtualXboxButtonMask.Up | VirtualXboxButtonMask.Left,
            _ => VirtualXboxButtonMask.None
        };
    }

    public static VirtualXboxButtonMask ToXboxMask(VirtualButtonTarget target) => target switch
    {
        VirtualButtonTarget.A => VirtualXboxButtonMask.A,
        VirtualButtonTarget.B => VirtualXboxButtonMask.B,
        VirtualButtonTarget.X => VirtualXboxButtonMask.X,
        VirtualButtonTarget.Y => VirtualXboxButtonMask.Y,
        VirtualButtonTarget.LeftShoulder => VirtualXboxButtonMask.LeftShoulder,
        VirtualButtonTarget.RightShoulder => VirtualXboxButtonMask.RightShoulder,
        VirtualButtonTarget.Back => VirtualXboxButtonMask.Back,
        VirtualButtonTarget.Start => VirtualXboxButtonMask.Start,
        VirtualButtonTarget.LeftThumb => VirtualXboxButtonMask.LeftThumb,
        VirtualButtonTarget.RightThumb => VirtualXboxButtonMask.RightThumb,
        VirtualButtonTarget.DPadUp => VirtualXboxButtonMask.Up,
        VirtualButtonTarget.DPadDown => VirtualXboxButtonMask.Down,
        VirtualButtonTarget.DPadLeft => VirtualXboxButtonMask.Left,
        VirtualButtonTarget.DPadRight => VirtualXboxButtonMask.Right,
        _ => VirtualXboxButtonMask.None
    };
}
