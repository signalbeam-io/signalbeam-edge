using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.BackgroundServices;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;
using SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Integration tests for background services like DeviceStatusMonitor.
/// </summary>
public class BackgroundServiceTests : IClassFixture<TelemetryProcessorTestFixture>
{
    private readonly TelemetryProcessorTestFixture _fixture;

    public BackgroundServiceTests(TelemetryProcessorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeviceHeartbeatRepository_GetInactiveDevices_ReturnsStaleDevices()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext();
        var deviceId = new DeviceId(Guid.NewGuid());

        // Create a device heartbeat that's old (stale)
        var staleHeartbeat = DeviceHeartbeat.Create(
            deviceId,
            DateTimeOffset.UtcNow.AddMinutes(-10), // 10 minutes ago
            "Online");

        dbContext.DeviceHeartbeats.Add(staleHeartbeat);
        await dbContext.SaveChangesAsync();

        var repository = new DeviceHeartbeatRepository(dbContext);

        // Act
        var inactiveDevices = await repository.GetInactiveDevicesAsync(
            TimeSpan.FromMinutes(5), // Threshold: 5 minutes
            CancellationToken.None);

        // Assert
        inactiveDevices.Should().Contain(deviceId.Value);
    }

    [Fact]
    public async Task DeviceHeartbeatRepository_GetInactiveDevices_ExcludesActiveDevices()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext();

        var staleDeviceId = new DeviceId(Guid.NewGuid());
        var activeDeviceId = new DeviceId(Guid.NewGuid());

        // Stale heartbeat (10 minutes ago)
        var staleHeartbeat = DeviceHeartbeat.Create(
            staleDeviceId,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            "Online");

        // Fresh heartbeat (30 seconds ago)
        var freshHeartbeat = DeviceHeartbeat.Create(
            activeDeviceId,
            DateTimeOffset.UtcNow.AddSeconds(-30),
            "Online");

        dbContext.DeviceHeartbeats.Add(staleHeartbeat);
        dbContext.DeviceHeartbeats.Add(freshHeartbeat);
        await dbContext.SaveChangesAsync();

        var repository = new DeviceHeartbeatRepository(dbContext);

        // Act
        var inactiveDevices = await repository.GetInactiveDevicesAsync(
            TimeSpan.FromMinutes(5), // Threshold: 5 minutes
            CancellationToken.None);

        // Assert
        inactiveDevices.Should().Contain(staleDeviceId.Value);
        inactiveDevices.Should().NotContain(activeDeviceId.Value);
    }

    [Fact]
    public async Task DeviceHeartbeatRepository_AddHeartbeat_StoresInDatabase()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext();
        var repository = new DeviceHeartbeatRepository(dbContext);

        var deviceId = new DeviceId(Guid.NewGuid());
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "Online");

        // Act
        await repository.AddAsync(heartbeat, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var saved = await dbContext.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId)
            .FirstOrDefaultAsync();

        saved.Should().NotBeNull();
        saved!.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task DeviceMetricsRepository_AddMetrics_StoresInDatabase()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext();
        var repository = new DeviceMetricsRepository(dbContext);

        var deviceId = new DeviceId(Guid.NewGuid());
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            45.5,  // CPU
            60.2,  // Memory
            75.8,  // Disk
            3600,  // Uptime
            3);    // Running containers

        // Act
        await repository.AddAsync(metrics, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var saved = await dbContext.DeviceMetrics
            .Where(m => m.DeviceId == deviceId)
            .FirstOrDefaultAsync();

        saved.Should().NotBeNull();
        saved!.DeviceId.Should().Be(deviceId);
        saved.CpuUsage.Should().Be(45.5);
        saved.MemoryUsage.Should().Be(60.2);
        saved.DiskUsage.Should().Be(75.8);
    }

    [Fact]
    public void MetricsAggregationService_Configuration_CanBeConfigured()
    {
        // Arrange
        var options = new MetricsAggregationOptions
        {
            Enabled = true,
            AggregationInterval = TimeSpan.FromMinutes(10)
        };

        // Assert
        options.Enabled.Should().BeTrue();
        options.AggregationInterval.Should().Be(TimeSpan.FromMinutes(10));
    }
}
