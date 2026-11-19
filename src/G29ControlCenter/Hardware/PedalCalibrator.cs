namespace G29ControlCenter.Hardware;

/// <summary>
/// Normiert eine invertierte Logitech-Pedalachse (32767 = ungedrückt 0 %, -32768 = voll 100 %)
/// optional mit automatischer laufender Min/Max-Kalibrierung.
/// </summary>
public sealed class PedalAxisCalibrator
{
    public const int NominalReleasedRaw = 32767;
    public const int NominalPressedRaw = -32768;
    public const int MinAutoCalibrationSpan = 6000;
    private const double BottomDeadbandPercent = 0.4;

    private bool _hasObservedSample;

    public bool AutoCalibrationEnabled { get; set; } = false;

    public int ObservedMaxRaw { get; private set; } = NominalReleasedRaw;
    public int ObservedMinRaw { get; private set; } = NominalPressedRaw;

    public double Normalize(int rawValue)
    {
        int clampedRaw = Math.Clamp(rawValue, NominalPressedRaw, NominalReleasedRaw);

        if (AutoCalibrationEnabled)
        {
            if (!_hasObservedSample)
            {
                ObservedMaxRaw = clampedRaw;
                ObservedMinRaw = clampedRaw;
                _hasObservedSample = true;
            }
            else
            {
                if (clampedRaw > ObservedMaxRaw)
                {
                    ObservedMaxRaw = clampedRaw;
                }

                if (clampedRaw < ObservedMinRaw)
                {
                    ObservedMinRaw = clampedRaw;
                }
            }
        }

        int releasedBound;
        int pressedBound;

        if (AutoCalibrationEnabled && _hasObservedSample && (ObservedMaxRaw - ObservedMinRaw) >= MinAutoCalibrationSpan)
        {
            releasedBound = ObservedMaxRaw;
            pressedBound = ObservedMinRaw;
        }
        else
        {
            releasedBound = _hasObservedSample
                ? Math.Max(NominalReleasedRaw, ObservedMaxRaw)
                : NominalReleasedRaw;
            pressedBound = NominalPressedRaw;
        }

        int span = Math.Max(1, releasedBound - pressedBound);
        double percent = Math.Clamp((double)(releasedBound - clampedRaw) / span * 100.0, 0.0, 100.0);
        return percent < BottomDeadbandPercent ? 0.0 : percent;
    }

    public void Reset()
    {
        _hasObservedSample = false;
        ObservedMaxRaw = NominalReleasedRaw;
        ObservedMinRaw = NominalPressedRaw;
    }
}

/// <summary>
/// Kalibrierungssatz für die drei Pedalachsen Gas, Bremse und Kupplung.
/// </summary>
public sealed class PedalCalibrationSet
{
    private bool _autoCalibrationEnabled = false;

    public PedalAxisCalibrator Gas { get; } = new();
    public PedalAxisCalibrator Brake { get; } = new();
    public PedalAxisCalibrator Clutch { get; } = new();

    public bool AutoCalibrationEnabled
    {
        get => _autoCalibrationEnabled;
        set
        {
            _autoCalibrationEnabled = value;
            Gas.AutoCalibrationEnabled = value;
            Brake.AutoCalibrationEnabled = value;
            Clutch.AutoCalibrationEnabled = value;
        }
    }

    public void ResetAll()
    {
        Gas.Reset();
        Brake.Reset();
        Clutch.Reset();
    }
}
