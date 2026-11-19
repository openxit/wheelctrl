using G29ControlCenter.Hardware;
using Xunit;

namespace G29ControlCenter.Tests;

public sealed class HardwareServiceTests
{
    [Fact]
    public void Service_InitializesStopsAndDisposesWithoutException()
    {
        using var service = new LogitechG29Service();
        service.Initialize(nint.Zero);

        WheelTelemetrySnapshot snapshot = service.LatestSnapshot;
        Assert.NotEqual(HardwareConnectionState.Initializing, snapshot.ConnectionState);

        service.EmergencyStop();
        Assert.Equal(0, service.LatestSnapshot.CommandedConstantForcePercent);
    }

    [Theory]
    [InlineData(uint.MaxValue, "Mitte")]
    [InlineData(65535u, "Mitte")]
    [InlineData(0u, "↑")]
    [InlineData(4500u, "↗")]
    [InlineData(9000u, "→")]
    [InlineData(13500u, "↘")]
    [InlineData(18000u, "↓")]
    [InlineData(22500u, "↙")]
    [InlineData(27000u, "←")]
    [InlineData(31500u, "↖")]
    public void DecodeDPadLabel_ReturnsExpectedDirection(uint povRaw, string expected)
    {
        Assert.Equal(expected, WheelTelemetrySnapshot.DecodeDPadLabel(povRaw));
    }

    [Fact]
    public void DecodeShifterGear_MapsAllGatesAndNeutral()
    {
        bool[] buttons = new bool[WheelTelemetrySnapshot.ButtonCount];
        Assert.Equal("N", WheelTelemetrySnapshot.DecodeShifterGear(buttons));

        for (int gear = 1; gear <= 6; gear++)
        {
            Array.Clear(buttons);
            buttons[11 + gear] = true;
            Assert.Equal(gear.ToString(), WheelTelemetrySnapshot.DecodeShifterGear(buttons));
        }

        Array.Clear(buttons);
        buttons[18] = true;
        Assert.Equal("R", WheelTelemetrySnapshot.DecodeShifterGear(buttons));
    }
}
