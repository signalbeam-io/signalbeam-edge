using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for DeviceHeartbeatRepository using Testcontainers.
/// Tests TimescaleDB hypertable functionality and time-series queries.
/// </summary>
[Collection("Database")]
public class DeviceHeartbeatRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private TelemetryDbContext? _context;
    private DeviceHeartbeatRepository? _repository;

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("telemetry_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        _context = new TelemetryDbContext(options);
        await _context.Database.MigrateAsync();

        _repository = new DeviceHeartbeatRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddAsync_ShouldPersistDeviceHeartbeat()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var timestamp = DateTimeOffset.UtcNow;
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            status: "online",
            ipAddress: "192.168.1.100"
        );

        // Act
        await _repository!.AddAsync(heartbeat);
        await _repository.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetLatestByDeviceAsync(deviceId);
        retrieved.Should().NotBeNull();
        retrieved!.DeviceId.Should().Be(deviceId);
        retrieved.Status.Should().Be("online");
        retrieved.IpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task GetByDeviceAndTimeRangeAsync_ShouldReturnHeartbeatsInRange()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);

        var heartbeats = new List<DeviceHeartbeat>
        {
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(1), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(2), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(3), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(-5), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(10), "online")
        };

        await _repository!.AddRangeAsync(heartbeats);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDeviceAndTimeRangeAsync(
            deviceId,
            baseTime,
            baseTime.AddHours(5)
        );

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(h => h.Timestamp);
    }

    [Fact]
    public async Task GetLatestByDeviceAsync_ShouldReturnMostRecentHeartbeat()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);

        var heartbeats = new List<DeviceHeartbeat>
        {
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(1), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(2), "online"),
            DeviceHeartbeat.Create(deviceId, baseTime.AddHours(3), "offline")
        };

        await _repository!.AddRangeAsync(heartbeats);
        await _repository.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestByDeviceAsync(deviceId);

        // Assert
        latest.Should().NotBeNull();
        latest!.Status.Should().Be("offline");
        latest.Timestamp.Should().BeCloseTo(baseTime.AddHours(3), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetLatestByDevicesAsync_ShouldReturnLatestForMultipleDevices()
    {
        // Arrange
        var device1Id = new DeviceId(Guid.NewGuid());
        var device2Id = new DeviceId(Guid.NewGuid());
        var device3Id = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);

        var heartbeats = new List<DeviceHeartbeat>
        {
            // Device 1
            DeviceHeartbeat.Create(device1Id, baseTime.AddHours(1), "online"),
            DeviceHeartbeat.Create(device1Id, baseTime.AddHours(2), "offline"),

            // Device 2
            DeviceHeartbeat.Create(device2Id, baseTime.AddHours(1), "online"),
            DeviceHeartbeat.Create(device2Id, baseTime.AddHours(3), "online"),

            // Device 3
            DeviceHeartbeat.Create(device3Id, baseTime.AddHours(1), "offline")
        };

        await _repository!.AddRangeAsync(heartbeats);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestByDevicesAsync(
            new[] { device1Id, device2Id, device3Id }
        );

        // Assert
        result.Should().HaveCount(3);
        result[device1Id.Value].Status.Should().Be("offline");
        result[device2Id.Value].Status.Should().Be("online");
        result[device3Id.Value].Status.Should().Be("offline");
    }

    [Fact]
    public async Task CountByDeviceAndTimeRangeAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);

        var heartbeats = new List<DeviceHeartbeat>();
        for (int i = 0; i < 20; i++)
        {
            heartbeats.Add(DeviceHeartbeat.Create(
                deviceId,
                baseTime.AddMinutes(i * 30),
                "online"
            ));
        }

        await _repository!.AddRangeAsync(heartbeats);
        await _repository.SaveChangesAsync();

        // Act
        var count = await _repository.CountByDeviceAndTimeRangeAsync(
            deviceId,
            baseTime,
            baseTime.AddHours(5)
        );

        // Assert
        count.Should().Be(10); // 5 hours * 2 heartbeats per hour
    }

    [Fact]
    public async Task GetInactiveDevicesAsync_ShouldReturnDevicesWithoutRecentHeartbeats()
    {
        // Arrange
        var activeDeviceId = new DeviceId(Guid.NewGuid());
        var inactiveDeviceId = new DeviceId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        var heartbeats = new List<DeviceHeartbeat>
        {
            // Active device (recent heartbeat)
            DeviceHeartbeat.Create(activeDeviceId, now.AddMinutes(-1), "online"),

            // Inactive device (old heartbeat)
            DeviceHeartbeat.Create(inactiveDeviceId, now.AddHours(-2), "online")
        };

        await _repository!.AddRangeAsync(heartbeats);
        await _repository.SaveChangesAsync();

        // Act
        var inactiveDevices = await _repository.GetInactiveDevicesAsync(
            inactivityThreshold: TimeSpan.FromMinutes(30)
        );

        // Assert
        inactiveDevices.Should().Contain(inactiveDeviceId.Value);
        inactiveDevices.Should().NotContain(activeDeviceId.Value);
    }
}
