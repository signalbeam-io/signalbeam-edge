using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services;
using SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Tests.AlertRules;

public class HighResourceUtilizationRuleTests
{
    private readonly IDeviceHeartbeatRepository _heartbeats = Substitute.For<IDeviceHeartbeatRepository>();
    private readonly IDeviceMetricsRepository _metrics = Substitute.For<IDeviceMetricsRepository>();
    private readonly IAlertRepository _alerts = Substitute.For<IAlertRepository>();
    private readonly IDeviceTenantResolver _tenants = Substitute.For<IDeviceTenantResolver>();
    private readonly TenantId _tenantId = new(Guid.NewGuid());
    private readonly AlertingOptions _options = new();
    private readonly DeviceId _deviceId = new(Guid.NewGuid());

    public HighResourceUtilizationRuleTests()
    {
        _heartbeats.GetActiveDeviceIdsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([_deviceId]);
        _tenants.ResolveAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(_tenantId);
    }

    [Fact]
    public async Task EvaluateAsync_SustainedDiskAboveThreshold_CreatesAlert()
    {
        // Arrange
        Samples((40, 50, 93), (55, 52, 95), (35, 51, 97));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        var alert = alerts.Should().ContainSingle().Subject;
        alert.Type.Should().Be(AlertType.HighResourceUtilization);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.DeviceId.Should().Be(_deviceId);
        alert.TenantId.Should().Be(_tenantId);
        alert.Title.Should().Contain("disk").And.NotContain("CPU");
    }

    [Fact]
    public async Task EvaluateAsync_CpuAndMemorySaturated_NamesBoth()
    {
        // Arrange
        Samples((97, 92, 20), (99, 95, 20));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().ContainSingle().Which.Title.Should().Contain("CPU and memory");
    }

    [Fact]
    public async Task EvaluateAsync_SingleSpike_DoesNotAlert()
    {
        // Arrange: one sample over the threshold isn't sustained.
        Samples((40, 50, 30), (99, 50, 30), (42, 50, 30));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_TooFewSamples_DoesNotAlert()
    {
        // Arrange
        Samples((99, 99, 99));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_ActiveAlertExists_DoesNotDuplicate()
    {
        // Arrange
        Samples((99, 50, 30), (99, 50, 30));
        _alerts.GetActiveAlertByDeviceAndTypeAsync(_deviceId, AlertType.HighResourceUtilization, Arg.Any<CancellationToken>())
            .Returns(Alert.Create(_tenantId, AlertSeverity.Warning, AlertType.HighResourceUtilization, "t", "d", _deviceId));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_UnknownTenant_SkipsAlert()
    {
        // Arrange
        Samples((99, 50, 30), (99, 50, 30));
        _tenants.ResolveAsync(_deviceId, Arg.Any<CancellationToken>()).Returns((TenantId?)null);

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Disabled_DoesNothing()
    {
        // Arrange
        _options.Rules.HighResourceUtilization.Enabled = false;
        Samples((99, 99, 99), (99, 99, 99));

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
        await _heartbeats.DidNotReceiveWithAnyArgs().GetActiveDeviceIdsAsync(default, default);
    }

    private HighResourceUtilizationRule Rule() => new(
        _heartbeats, _metrics, _alerts, NullLogger<HighResourceUtilizationRule>.Instance, Options.Create(_options), _tenants);

    private void Samples(params (double Cpu, double Memory, double Disk)[] samples)
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-3);
        var metrics = samples
            .Select((s, i) => DeviceMetrics.Create(_deviceId, start.AddSeconds(30 * i), s.Cpu, s.Memory, s.Disk, 3600, 2))
            .ToList();

        _metrics.GetByDeviceIdAndTimeRangeAsync(_deviceId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(metrics);
    }
}
