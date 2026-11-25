using FluentAssertions;
using SignalBeam.EdgeAgent.Simulator;

namespace SignalBeam.EdgeAgent.Tests.Unit.Simulator;

public class SimulatorOptionsTests
{
    private static readonly DeviceLoopOptions Loop = new(
        HeartbeatInterval: TimeSpan.FromSeconds(30),
        MetricsInterval: TimeSpan.FromSeconds(30),
        DesiredStateInterval: TimeSpan.FromSeconds(60),
        Iterations: 0,
        ReportState: true,
        Jitter: 0,
        LogSuccess: true);

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void ValidateOptions_ValidSettings_ReturnsNull(int fleetSize)
    {
        // Act
        var error = Program.ValidateOptions(fleetSize, [], Loop);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateOptions_SingleDeviceOptionsWithSingleDevice_AreAllowed()
    {
        // Act
        var error = Program.ValidateOptions(1, ["--use-device-auth", "--device-id"], Loop);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateOptions_SingleDeviceOptionsInFleetMode_NamesTheOptions()
    {
        // Act
        var error = Program.ValidateOptions(5, ["--use-device-auth", "--metadata"], Loop);

        // Assert
        error.Should().Contain("--use-device-auth").And.Contain("--metadata");
    }

    [Fact]
    public void ValidateOptions_FleetSizeBelowOne_IsRejected()
    {
        // Act
        var error = Program.ValidateOptions(0, [], Loop);

        // Assert
        error.Should().Contain("--fleet-size");
    }

    [Fact]
    public void ValidateOptions_ZeroInterval_IsRejected()
    {
        // Act
        var error = Program.ValidateOptions(1, [], Loop with { MetricsInterval = TimeSpan.Zero });

        // Assert
        error.Should().Contain("intervals");
    }

    [Fact]
    public void Jitter_StaysWithinBoundsAndIsReproducible()
    {
        // Arrange
        var interval = TimeSpan.FromSeconds(30);

        // Act
        var first = Sample(new Random(42));
        var second = Sample(new Random(42));

        // Assert
        first.Should().OnlyContain(t => t >= TimeSpan.FromSeconds(27) && t <= TimeSpan.FromSeconds(33));
        second.Should().Equal(first);
        SimulatedDevice.Jitter(interval, new Random(1), 0).Should().Be(interval);
    }

    private static List<TimeSpan> Sample(Random rng) =>
        Enumerable.Range(0, 1000).Select(_ => SimulatedDevice.Jitter(TimeSpan.FromSeconds(30), rng, 0.1)).ToList();
}
