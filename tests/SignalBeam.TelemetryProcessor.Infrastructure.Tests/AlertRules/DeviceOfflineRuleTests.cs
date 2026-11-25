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

public class DeviceOfflineRuleTests
{
    private readonly IDeviceHeartbeatRepository _heartbeats = Substitute.For<IDeviceHeartbeatRepository>();
    private readonly IAlertRepository _alerts = Substitute.For<IAlertRepository>();
    private readonly IDeviceTenantResolver _tenants = Substitute.For<IDeviceTenantResolver>();
    private readonly DeviceId _deviceId = new(Guid.NewGuid());

    public DeviceOfflineRuleTests()
    {
        _heartbeats.GetStaleDevicesAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns([_deviceId]);
        _heartbeats.GetLatestByDeviceIdAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(DeviceHeartbeat.Create(_deviceId, DateTimeOffset.UtcNow.AddMinutes(-8), "Online"));
    }

    [Fact]
    public async Task EvaluateAsync_StaleDeviceWithKnownTenant_CreatesAlertForThatTenant()
    {
        // Arrange: this used to throw on new TenantId(Guid.Empty), so no offline alert was ever created.
        var tenantId = new TenantId(Guid.NewGuid());
        _tenants.ResolveAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(tenantId);

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        var alert = alerts.Should().ContainSingle().Subject;
        alert.Type.Should().Be(AlertType.DeviceOffline);
        alert.TenantId.Should().Be(tenantId);
        alert.DeviceId.Should().Be(_deviceId);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownTenant_SkipsAlert()
    {
        // Arrange
        _tenants.ResolveAsync(_deviceId, Arg.Any<CancellationToken>()).Returns((TenantId?)null);

        // Act
        var alerts = await Rule().EvaluateAsync();

        // Assert
        alerts.Should().BeEmpty();
    }

    private DeviceOfflineRule Rule() => new(
        _heartbeats, _alerts, NullLogger<DeviceOfflineRule>.Instance, Options.Create(new AlertingOptions()), _tenants);
}
