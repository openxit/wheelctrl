namespace G29ControlCenter.Models;

/// <summary>
/// Optionale Kopplung des Gaspedals an Lenkrad-Sollwert oder Vibration.
/// </summary>
public enum PedalCouplingMode
{
    None = 0,
    GasToTargetAngle = 1,
    GasToBumpyVibration = 2
}

/// <summary>
/// Betriebsart für die RPM-Schalt-LEDs am Lenkradkranz.
/// </summary>
public enum RpmLedMode
{
    Manual = 0,
    AcceleratorCoupled = 1
}

/// <summary>
/// Unveränderlicher Konfigurationssatz für das Force-Feedback-Effekt-Labor.
/// </summary>
public sealed record FfbEffectState
{
    public bool SpringEnabled { get; init; }
    public int SpringOffsetPercent { get; init; }
    public int SpringSaturationPercent { get; init; } = 70;
    public int SpringCoefficientPercent { get; init; } = 60;

    public bool DamperEnabled { get; init; }
    public int DamperCoefficientPercent { get; init; } = 40;

    public bool BumpyRoadEnabled { get; init; }
    public int BumpyRoadMagnitudePercent { get; init; } = 35;

    public bool DirtRoadEnabled { get; init; }
    public int DirtRoadMagnitudePercent { get; init; } = 30;

    public int CollisionMagnitudePercent { get; init; } = 50;
}
