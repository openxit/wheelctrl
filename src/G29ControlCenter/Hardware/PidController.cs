namespace G29ControlCenter.Hardware;

/// <summary>
/// Stabiler PI-D-Regler für die aktive G29-Winkelpositionierung.
/// Verwendet Derivative-on-Measurement (kein Derivative-Kick bei Sollwertsprüngen),
/// Tiefpassfilterung auf der Geschwindigkeit sowie einen Anti-Windup-Integralanteil
/// zur Überwindung der mechanischen Getriebe-Haftreibung des G29.
/// </summary>
public sealed class PidController
{
    private double _kp = 1.25;
    private double _ki = 1.25;
    private double _kd = 0.085;
    private double _deadzoneDegrees = 0.5;
    private int _maxForcePercent = 50;
    private double _velocityFilterTauSeconds = 0.025;

    private bool _hasPreviousAngle;
    private double _previousActualAngle;
    private double _filteredVelocityDegPerSec;
    private double _integralErrorDegSec;
    private int _previousErrorSign;

    public double Kp
    {
        get => _kp;
        set => _kp = Math.Clamp(value, 0.05, 5.0);
    }

    public double Ki
    {
        get => _ki;
        set => _ki = Math.Clamp(value, 0.0, 3.0);
    }

    public double Kd
    {
        get => _kd;
        set => _kd = Math.Clamp(value, 0.0, 0.5);
    }

    public double DeadzoneDegrees
    {
        get => _deadzoneDegrees;
        set => _deadzoneDegrees = Math.Clamp(value, 0.0, 5.0);
    }

    public int MaxForcePercent
    {
        get => _maxForcePercent;
        set => _maxForcePercent = Math.Clamp(value, 10, 100);
    }

    public double VelocityFilterTauSeconds
    {
        get => _velocityFilterTauSeconds;
        set => _velocityFilterTauSeconds = Math.Clamp(value, 0.005, 0.25);
    }

    public double LastErrorDegrees { get; private set; }
    public double LastFilteredVelocityDegPerSec { get; private set; }
    public int LastOutputForcePercent { get; private set; }

    /// <summary>
    /// Berechnet die benötigte ConstantForce-Stellgröße [-MaxForcePercent, +MaxForcePercent]
    /// in nativer Logitech-G29-Polarität (negativ = Rechtslauf / +Grad, positiv = Linkslauf / -Grad).
    /// </summary>
    public int Compute(double targetAngleDeg, double actualAngleDeg, double deltaSeconds)
    {
        double dt = double.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0.002, 0.050) : 0.010;
        double error = targetAngleDeg - actualAngleDeg;

        if (!_hasPreviousAngle)
        {
            _previousActualAngle = actualAngleDeg;
            _filteredVelocityDegPerSec = 0.0;
            _hasPreviousAngle = true;
        }
        else
        {
            double rawVelocity = (actualAngleDeg - _previousActualAngle) / dt;
            double alpha = dt / (_velocityFilterTauSeconds + dt);
            _filteredVelocityDegPerSec = (alpha * rawVelocity) + ((1.0 - alpha) * _filteredVelocityDegPerSec);
            _previousActualAngle = actualAngleDeg;
        }

        LastErrorDegrees = error;
        LastFilteredVelocityDegPerSec = _filteredVelocityDegPerSec;

        if (Math.Abs(error) <= _deadzoneDegrees && Math.Abs(_filteredVelocityDegPerSec) < 12.0)
        {
            _integralErrorDegSec = 0.0;
            _previousErrorSign = 0;
            LastOutputForcePercent = 0;
            return 0;
        }

        double effectiveError = Math.Abs(error) <= _deadzoneDegrees
            ? 0.0
            : Math.Sign(error) * (Math.Abs(error) - _deadzoneDegrees);

        int currentSign = Math.Sign(effectiveError);
        if (currentSign != 0 && _previousErrorSign != 0 && currentSign != _previousErrorSign)
        {
            // Beim Überfahren des Ziels Integralanteil sofort dämpfen
            _integralErrorDegSec *= 0.25;
        }
        _previousErrorSign = currentSign;

        if (Math.Abs(effectiveError) < 30.0)
        {
            _integralErrorDegSec = Math.Clamp(_integralErrorDegSec + (effectiveError * dt), -30.0, 30.0);
        }
        else
        {
            _integralErrorDegSec *= Math.Exp(-dt * 4.0);
        }

        double rawForce = (_kp * effectiveError) + (_ki * _integralErrorDegSec) - (_kd * _filteredVelocityDegPerSec);
        double clampedForce = Math.Clamp(rawForce, -_maxForcePercent, _maxForcePercent);

        // Logitech Steering Wheel SDK (G29 unter Windows/DirectInput):
        // Negative ConstantForce-Werte ziehen nach rechts (+Grad), positive nach links (-Grad).
        double g29DirectedForce = -clampedForce;

        int output = (int)Math.Round(g29DirectedForce);
        LastOutputForcePercent = output;
        return output;
    }

    public void Reset()
    {
        _hasPreviousAngle = false;
        _previousActualAngle = 0.0;
        _filteredVelocityDegPerSec = 0.0;
        _integralErrorDegSec = 0.0;
        _previousErrorSign = 0;
        LastErrorDegrees = 0.0;
        LastFilteredVelocityDegPerSec = 0.0;
        LastOutputForcePercent = 0;
    }
}
