using G29ControlCenter.Hardware;
using Xunit;

namespace G29ControlCenter.Tests;

public sealed class PedalCalibratorTests
{
    [Fact]
    public void InvertedRawAxis_MapsNominallyToZeroFiftyAndHundredPercent()
    {
        var cal = new PedalAxisCalibrator { AutoCalibrationEnabled = false };

        Assert.Equal(0.0, cal.Normalize(32767), precision: 1);
        Assert.InRange(cal.Normalize(0), 49.9, 50.1);
        Assert.Equal(100.0, cal.Normalize(-32768), precision: 1);
    }

    [Fact]
    public void AutoCalibration_AdaptsToShorterMechanicalTravel()
    {
        var cal = new PedalAxisCalibrator { AutoCalibrationEnabled = true };

        // Vor voller Auslenkung liegt noch kein ausreichender Hub vor -> stabil nahe 0 %
        Assert.InRange(cal.Normalize(32000), 0.0, 2.0);

        // Pedal wird bis -16000 gedrückt und wieder bis 32000 losgelassen
        Assert.Equal(100.0, cal.Normalize(-16000), precision: 1);
        Assert.Equal(0.0, cal.Normalize(32000), precision: 1);

        // Mittelpunkt zwischen 32000 und -16000 ist 8000 -> ~50 %
        double midPct = cal.Normalize(8000);
        Assert.InRange(midPct, 49.5, 50.5);

        cal.Reset();
        Assert.Equal(PedalAxisCalibrator.NominalReleasedRaw, cal.ObservedMaxRaw);
        Assert.Equal(PedalAxisCalibrator.NominalPressedRaw, cal.ObservedMinRaw);
    }

    [Fact]
    public void UntouchedNoisyPedal_DoesNotFalselyScaleToHundredPercent()
    {
        var cal = new PedalAxisCalibrator { AutoCalibrationEnabled = true };

        Assert.Equal(0.0, cal.Normalize(32767), precision: 2);
        double noisyPct = cal.Normalize(32550);
        Assert.InRange(noisyPct, 0.0, 1.0);
    }
}
