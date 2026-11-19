namespace G29ControlCenter.Hardware;

public enum HardwareConnectionState
{
    Initializing = 0,
    Connected = 1,
    WaitingForDevice = 2,
    DriverUnavailable = 3
}

public enum MotorPowerState
{
    Disconnected = 0,
    UnknownReady = 1,
    Verified24V = 2,
    NoResponseCheck24V = 3
}

/// <summary>
/// Unveränderlicher Echtzeit-Schnappschuss der gesamten Hardware- und Regler-Telemetrie.
/// </summary>
public sealed record WheelTelemetrySnapshot
{
    public const int ButtonCount = 25;

    public HardwareConnectionState ConnectionState { get; init; } = HardwareConnectionState.Initializing;
    public int ControllerIndex { get; init; } = -1;
    public string LoadedSdkPath { get; init; } = string.Empty;
    public string DeviceName { get; init; } = "Suche Logitech G29...";
    public string StatusMessage { get; init; } = string.Empty;
    public bool IsWindowFocused { get; init; } = true;
    public bool HasForceFeedback { get; init; }

    public MotorPowerState MotorPowerState { get; init; } = MotorPowerState.Disconnected;
    public string MotorStatusHint { get; init; } = "Nicht verbunden";
    public bool PolarityWarningActive { get; init; }

    public int OperatingRangeDegrees { get; init; } = 900;
    public double WheelAngleDegrees { get; init; }
    public double WheelPercent { get; init; }
    public int WheelRawX { get; init; }
    public double TargetAngleDegrees { get; init; }

    public double GasPercent { get; init; }
    public int GasRaw { get; init; } = PedalAxisCalibrator.NominalReleasedRaw;
    public double BrakePercent { get; init; }
    public int BrakeRaw { get; init; } = PedalAxisCalibrator.NominalReleasedRaw;
    public double ClutchPercent { get; init; }
    public int ClutchRaw { get; init; } = PedalAxisCalibrator.NominalReleasedRaw;

    public bool[] Buttons { get; init; } = new bool[ButtonCount];
    public uint PovRaw { get; init; } = uint.MaxValue;
    public string DPadLabel { get; init; } = "Mitte";
    public string ShifterGear { get; init; } = "N";

    public int CommandedConstantForcePercent { get; init; }
    public double RpmLedNormalized { get; init; }
    public double ActualControlLoopHz { get; init; }
    public bool CombinedPedalsWarning { get; init; }

    public static string DecodeShifterGear(ReadOnlySpan<bool> buttons)
    {
        if (buttons.Length > 18 && buttons[18])
        {
            return "R";
        }

        for (int gear = 1; gear <= 6; gear++)
        {
            int idx = 11 + gear;
            if (buttons.Length > idx && buttons[idx])
            {
                return gear.ToString();
            }
        }

        return "N";
    }

    public static string DecodeDPadLabel(uint povRaw)
    {
        if (povRaw == uint.MaxValue || povRaw == 65535)
        {
            return "Mitte";
        }

        return povRaw switch
        {
            0 => "↑",
            4500 => "↗",
            9000 => "→",
            13500 => "↘",
            18000 => "↓",
            22500 => "↙",
            27000 => "←",
            31500 => "↖",
            _ => $"{povRaw / 100.0:0}°"
        };
    }
}
