using G29ControlCenter.Models;

namespace G29ControlCenter.Hardware;

public interface IWheelHardwareService : IDisposable
{
    event Action<WheelTelemetrySnapshot>? TelemetryUpdated;

    WheelTelemetrySnapshot LatestSnapshot { get; }

    void Initialize(nint windowHandle);
    void TryReconnect();
    void SetWindowFocused(bool focused);
    void SetOperatingRange(int rangeDegrees);

    void UpdateControlParameters(
        ControlMode controlMode,
        double targetAngleDegrees,
        bool freewheelGentleDampingEnabled,
        int freewheelDampingPercent,
        FfbEffectState ffbEffects,
        PedalCouplingMode pedalCouplingMode,
        RpmLedMode rpmLedMode,
        double manualRpmPercent);

    void UpdatePidParameters(
        double kp,
        double kd,
        double deadzoneDegrees,
        int maxForcePercent);

    void SetPedalAutoCalibration(bool enabled);
    void ResetPedalCalibration();

    void TriggerFrontalCollision(int magnitudePercent);
    void EmergencyStop();
}
