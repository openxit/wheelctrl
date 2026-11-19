using G29ControlCenter.Hardware;
using Xunit;

namespace G29ControlCenter.Tests;

public sealed class PidControllerTests
{
    [Theory]
    [InlineData(0.0, 180.0)]
    [InlineData(180.0, -90.0)]
    [InlineData(-270.0, 270.0)]
    public void StepResponse_ConvergesCleanlyWithMinimalOvershootAndFriction(double startAngle, double targetAngle)
    {
        var pid = new PidController
        {
            Kp = 1.25,
            Ki = 1.25,
            Kd = 0.085,
            DeadzoneDegrees = 0.5,
            MaxForcePercent = 50
        };

        const double dt = 0.010;
        const double inertia = 0.085;
        const double viscousDamping = 0.65;
        const double staticFrictionTorque = 45.0; // Simuliert mechanische G29-Getriebe-Haftreibung

        double angle = startAngle;
        double velocity = 0.0;
        double peakOvershoot = 0.0;
        double direction = Math.Sign(targetAngle - startAngle);

        for (int step = 0; step < 350; step++)
        {
            int forcePct = pid.Compute(targetAngle, angle, dt);
            Assert.InRange(forcePct, -50, 50);

            // Echte G29-SDK-Polarität: negative ConstantForce dreht nach rechts (+Grad)
            double motorTorque = -forcePct * 11.5;

            double friction = Math.Abs(velocity) < 1.0
                ? Math.Clamp(motorTorque, -staticFrictionTorque, staticFrictionTorque)
                : Math.Sign(velocity) * staticFrictionTorque * 0.6;

            double netTorque = motorTorque - friction - (viscousDamping * velocity);
            velocity += (netTorque / inertia) * dt;
            angle += velocity * dt;

            if (direction > 0 && angle > targetAngle)
            {
                peakOvershoot = Math.Max(peakOvershoot, angle - targetAngle);
            }
            else if (direction < 0 && angle < targetAngle)
            {
                peakOvershoot = Math.Max(peakOvershoot, targetAngle - angle);
            }
        }

        double stepAmplitude = Math.Abs(targetAngle - startAngle);
        Assert.True(peakOvershoot < stepAmplitude * 0.06, $"Überschwingen {peakOvershoot:0.00}° überschreitet 6 %.");
        Assert.InRange(Math.Abs(targetAngle - angle), 0.0, 0.7);
    }

    [Fact]
    public void InstantaneousSetpointJump_HasZeroDerivativeKickAndUsesG29Polarity()
    {
        var pid = new PidController { Kp = 1.25, Ki = 0.75, Kd = 0.085, MaxForcePercent = 50 };

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(0, pid.Compute(0.0, 0.0, 0.010));
        }

        // Ziel +200° (rechts) bei 0° Ist -> G29 ConstantForce muss -50 sein (Rechtslauf)
        int output = pid.Compute(200.0, 0.0, 0.010);
        Assert.Equal(-50, output);
    }

    [Fact]
    public void InsideDeadzoneAtRest_ReturnsExactZeroForce()
    {
        var pid = new PidController { DeadzoneDegrees = 0.5, MaxForcePercent = 50 };

        Assert.Equal(0, pid.Compute(90.0, 90.0, 0.010));
        pid.Reset();
        Assert.Equal(0, pid.Compute(90.0, 90.4, 0.010));
        pid.Reset();
        Assert.Equal(0, pid.Compute(90.0, 89.6, 0.010));
    }
}
