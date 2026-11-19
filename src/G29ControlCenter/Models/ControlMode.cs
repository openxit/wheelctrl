namespace G29ControlCenter.Models;

/// <summary>
/// Betriebsmodus für die bidirektionale Lenkrad-Steuerung.
/// </summary>
public enum ControlMode
{
    /// <summary>
    /// Freilauf / Telemetrie: Keine aktive Zielwinkel-Kraft, optional sanfte Dämpfung.
    /// </summary>
    TelemetryFreewheel = 0,

    /// <summary>
    /// Aktives Nachführen auf einen Zielwinkel mittels PD-Regelschleife.
    /// </summary>
    MotorPositioning = 1
}
