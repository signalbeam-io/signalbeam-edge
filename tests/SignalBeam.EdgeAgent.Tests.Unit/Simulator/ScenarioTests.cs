using FluentAssertions;
using SignalBeam.EdgeAgent.Simulator;

namespace SignalBeam.EdgeAgent.Tests.Unit.Simulator;

public class ScenarioTests
{
    private static readonly IReadOnlyList<DeviceSpec> Fleet = FleetPlan.Create(20, seed: 42);
    private static readonly TimeSpan Start = TimeSpan.FromMinutes(2);

    [Fact]
    public void Assign_SameSeed_PicksSameDevices()
    {
        // Arrange
        var scenarios = ScenarioPlan.FromFlags(["offline", "disk-fill"], 2, Start, null);

        // Act
        var first = ScenarioPlan.Assign(Fleet, scenarios, 42);
        var second = ScenarioPlan.Assign(Fleet, scenarios, 42);

        // Assert
        first.Should().HaveCount(4).And.BeEquivalentTo(second);
        first.Values.Count(f => f.Scenario == ScenarioNames.Offline).Should().Be(2);
        first.Values.Count(f => f.Scenario == ScenarioNames.DiskFill).Should().Be(2);
    }

    [Fact]
    public void Assign_DifferentSeed_PicksOtherDevices()
    {
        // Arrange
        var scenarios = ScenarioPlan.FromFlags(["offline"], 3, Start, null);

        // Act
        var a = ScenarioPlan.Assign(Fleet, scenarios, 1).Keys;
        var b = ScenarioPlan.Assign(Fleet, scenarios, 2).Keys;

        // Assert
        a.Should().NotBeEquivalentTo(b);
    }

    [Fact]
    public void FromFlags_CommaSeparatedNames_UseScenarioDefaults()
    {
        // Act
        var scenarios = ScenarioPlan.FromFlags(["CPU-Spike, offline"], 1, Start, null);

        // Assert
        scenarios.Should().Equal(
            new ScenarioSpec(ScenarioNames.CpuSpike, 1, Start, TimeSpan.FromMinutes(10)),
            new ScenarioSpec(ScenarioNames.Offline, 1, Start, null));
    }

    [Fact]
    public void FromJson_ReadsEntriesWithDefaults()
    {
        // Arrange
        const string json = """
            {
              // comments and trailing commas are fine
              "scenarios": [
                { "name": "disk-fill", "devices": 2, "startSeconds": 60 },
                { "name": "memory-spike", "durationSeconds": 300 },
              ]
            }
            """;

        // Act
        var scenarios = ScenarioPlan.FromJson(json, Start);

        // Assert
        scenarios.Should().Equal(
            new ScenarioSpec(ScenarioNames.DiskFill, 2, TimeSpan.FromSeconds(60), null),
            new ScenarioSpec(ScenarioNames.MemorySpike, 1, Start, TimeSpan.FromSeconds(300)));
    }

    [Theory]
    [InlineData("meltdown", 1, 20, "Unknown scenario")]
    [InlineData("offline", 0, 20, "at least 1 device")]
    [InlineData("offline", 25, 20, "need 25 devices")]
    [InlineData("offline", 1, 1, "fleet mode")]
    public void Validate_RejectsBadScenarios(string name, int devices, int fleetSize, string expected)
    {
        // Act
        var error = ScenarioPlan.Validate([new ScenarioSpec(name, devices, Start, null)], fleetSize);

        // Assert
        error.Should().Contain(expected);
    }

    [Fact]
    public void LoadScenarios_MissingFile_ReturnsError()
    {
        // Act
        var (_, error) = Program.LoadScenarios("/nonexistent/scenarios.json", [], 1, Start, null, 20);

        // Assert
        error.Should().Contain("Could not read scenario file");
    }

    [Fact]
    public void Offline_IsOfflineFromStartUntilStopped()
    {
        // Arrange
        var fault = new DeviceFault(ScenarioNames.Offline, Start, null);

        // Assert
        fault.IsOffline(Start - TimeSpan.FromSeconds(1)).Should().BeFalse();
        fault.IsOffline(Start).Should().BeTrue();
        fault.IsOffline(Start + TimeSpan.FromHours(5)).Should().BeTrue();
    }

    [Fact]
    public void Flapping_AlternatesHalfPeriods()
    {
        // Arrange
        var fault = new DeviceFault(ScenarioNames.Flapping, Start, null);
        var half = DeviceFault.FlapPeriod / 2;

        // Assert
        fault.IsOffline(Start + TimeSpan.FromSeconds(10)).Should().BeTrue();
        fault.IsOffline(Start + half + TimeSpan.FromSeconds(10)).Should().BeFalse();
        fault.IsOffline(Start + DeviceFault.FlapPeriod + TimeSpan.FromSeconds(10)).Should().BeTrue();
        half.Should().BeGreaterThan(TimeSpan.FromMinutes(3), "offline phases must outlast the 2-min stale threshold plus the 1-min check");
    }

    [Fact]
    public void CpuSpike_RaisesCpuOnlyWhileActive()
    {
        // Arrange
        var fault = new DeviceFault(ScenarioNames.CpuSpike, Start, TimeSpan.FromMinutes(10));
        var normal = new MetricsSample(30, 40, 20);
        var rng = new Random(1);

        // Act
        var during = fault.Apply(normal, Start + TimeSpan.FromMinutes(1), rng);
        var after = fault.Apply(normal, Start + TimeSpan.FromMinutes(11), rng);

        // Assert
        during.Cpu.Should().BeInRange(96, 99.5);
        during.Memory.Should().Be(40);
        after.Should().Be(normal);
    }

    [Fact]
    public void DiskFill_RampsUpThenStaysAboveAlertThreshold()
    {
        // Arrange
        var fault = new DeviceFault(ScenarioNames.DiskFill, Start, null);
        var normal = new MetricsSample(30, 40, 20);
        var rng = new Random(1);

        // Act
        var early = fault.Apply(normal, Start + TimeSpan.FromSeconds(30), rng).Disk;
        var full = fault.Apply(normal, Start + DeviceFault.DiskFillRamp + TimeSpan.FromMinutes(1), rng).Disk;

        // Assert
        early.Should().BeLessThan(40);
        full.Should().BeInRange(99, 99.9);
    }

    [Fact]
    public void DeployFailure_FailsDeploymentsOnlyWhileActive()
    {
        // Arrange
        var fault = new DeviceFault(ScenarioNames.DeployFailure, Start, TimeSpan.FromMinutes(5));

        // Assert
        fault.FailsDeployments(Start + TimeSpan.FromMinutes(1)).Should().BeTrue();
        fault.FailsDeployments(Start + TimeSpan.FromMinutes(6)).Should().BeFalse();
        fault.IsOffline(Start + TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("tcp://localhost:4222", "nats://localhost:4222")]
    [InlineData("nats://nats:4222", "nats://nats:4222")]
    [InlineData("localhost:4222", "nats://localhost:4222")]
    public void NormalizeUrl_AcceptsAspireAndPlainEndpoints(string input, string expected)
    {
        NatsTelemetryPublisher.NormalizeUrl(input).Should().Be(expected);
    }
}
