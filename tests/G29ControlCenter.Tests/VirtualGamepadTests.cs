using G29ControlCenter.Hardware;
using G29ControlCenter.Models;
using Xunit;

namespace G29ControlCenter.Tests;

public sealed class VirtualGamepadTests
{
    [Fact]
    public void MapSteeringToShort_RespectsDeadzoneAndLockRange()
    {
        // Inside 2% deadzone of 270° (half = 135°, 2% = 2.7°) -> 0
        short insideDeadzone = VirtualGamepadReportMapper.MapSteeringToShort(2.0, 270, 2.0, 1.0, false);
        Assert.Equal(0, insideDeadzone);

        // Full right (+135° at 270° lock) -> short.MaxValue
        short fullRight = VirtualGamepadReportMapper.MapSteeringToShort(135.0, 270, 0.0, 1.0, false);
        Assert.Equal(short.MaxValue, fullRight);

        // Full left (-135° at 270° lock) -> short.MinValue
        short fullLeft = VirtualGamepadReportMapper.MapSteeringToShort(-135.0, 270, 0.0, 1.0, false);
        Assert.Equal(short.MinValue, fullLeft);

        // Inverted full right -> short.MinValue
        short invertedRight = VirtualGamepadReportMapper.MapSteeringToShort(135.0, 270, 0.0, 1.0, true);
        Assert.Equal(short.MinValue, invertedRight);
    }

    [Fact]
    public void Map_TranslatesPedalsButtonsAndDPadCorrectly()
    {
        var config = new VirtualGamepadMappingConfig
        {
            SteeringLockDegrees = 360,
            SteeringDeadzonePercent = 0.0,
            GasAxisTarget = VirtualAxisTarget.RightTrigger,
            BrakeAxisTarget = VirtualAxisTarget.LeftTrigger,
            ClutchAxisTarget = VirtualAxisTarget.Disabled,
            MapDPadToXboxDPad = true
        };

        // Custom remap button #12 (Shifter G1) -> Start
        config.ButtonMappings[12].TargetButton = VirtualButtonTarget.Start;

        bool[] buttons = new bool[WheelTelemetrySnapshot.ButtonCount];
        buttons[0] = true;  // ✕ -> default A
        buttons[12] = true; // G1 -> custom Start

        var snapshot = new WheelTelemetrySnapshot
        {
            WheelAngleDegrees = 90.0, // half of 180° half-lock
            GasPercent = 100.0,
            BrakePercent = 50.0,
            ClutchPercent = 100.0, // disabled
            PovRaw = 9000,         // Right
            Buttons = buttons
        };

        VirtualXbox360Report report = VirtualGamepadReportMapper.Map(snapshot, config);

        Assert.InRange(report.LeftStickX, 16300, 16500);
        Assert.Equal(255, report.RightTrigger);
        Assert.InRange(report.LeftTrigger, 127, 128);
        Assert.True((report.Buttons & VirtualXboxButtonMask.A) != 0);
        Assert.True((report.Buttons & VirtualXboxButtonMask.Start) != 0);
        Assert.True((report.Buttons & VirtualXboxButtonMask.Right) != 0);
    }

    [Fact]
    public void OverlaySettings_ClampsScalePercentToMinAndMax()
    {
        var settings = new OverlaySettings();
        settings.ScalePercent = 10;
        Assert.Equal(40, settings.ScalePercent);

        settings.ScalePercent = 500;
        Assert.Equal(250, settings.ScalePercent);
    }
}
