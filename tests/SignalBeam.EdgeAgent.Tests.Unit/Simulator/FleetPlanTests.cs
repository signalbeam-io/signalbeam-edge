using FluentAssertions;
using SignalBeam.EdgeAgent.Simulator;

namespace SignalBeam.EdgeAgent.Tests.Unit.Simulator;

public class FleetPlanTests
{
    [Fact]
    public void Create_SameSeed_ProducesIdenticalFleet()
    {
        // Act
        var first = FleetPlan.Create(20, seed: 42);
        var second = FleetPlan.Create(20, seed: 42);

        // Assert
        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Create_DifferentSeed_ProducesDifferentDevices()
    {
        // Act
        var a = FleetPlan.Create(5, seed: 1);
        var b = FleetPlan.Create(5, seed: 2);

        // Assert
        a.Select(d => d.DeviceId).Should().NotIntersectWith(b.Select(d => d.DeviceId));
    }

    [Fact]
    public void Create_LargerFleet_KeepsTheFirstDevicesOfASmallerOne()
    {
        // Growing the fleet with the same seed must not reshuffle existing devices.
        // Act
        var small = FleetPlan.Create(5, seed: 42);
        var large = FleetPlan.Create(20, seed: 42);

        // Assert
        large.Take(5).Should().BeEquivalentTo(small, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Create_Fleet_SpreadsDevicesAcrossGroupsAndTags()
    {
        // Act
        var fleet = FleetPlan.Create(20, seed: 42);

        // Assert
        fleet.Select(d => d.Environment).Distinct().Should().BeEquivalentTo(FleetPlan.Environments);
        fleet.Select(d => d.Hardware).Distinct().Should().BeEquivalentTo("rpi", "x86");
        fleet.Should().OnlyContain(d =>
            d.Tags.Contains($"environment={d.Environment}") && d.Tags.Contains($"hardware={d.Hardware}"));
    }

    [Fact]
    public void Create_Fleet_HasUniqueVersion4IdsAndNames()
    {
        // Act
        var fleet = FleetPlan.Create(200, seed: 7);

        // Assert
        fleet.Select(d => d.DeviceId).Should().OnlyHaveUniqueItems();
        fleet.Select(d => d.Name).Should().OnlyHaveUniqueItems();
        fleet.Should().OnlyContain(d => d.DeviceId.Version == 4);
        fleet.Should().OnlyContain(d => d.StartOffsetFraction >= 0 && d.StartOffsetFraction < 1);
    }

    [Fact]
    public void Create_FleetSizeBelowOne_Throws()
    {
        // Act
        var act = () => FleetPlan.Create(0, seed: 42);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
