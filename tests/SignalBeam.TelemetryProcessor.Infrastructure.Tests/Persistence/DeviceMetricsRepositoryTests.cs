using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for DeviceMetricsRepository using Testcontainers.
/// Tests TimescaleDB hypertable functionality and time-series queries.
/// </summary>
[Collection("Database")]
public class DeviceMetricsRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private TelemetryDbContext? _context;
    private DeviceMetricsRepository? _repository;

    public async Task InitializeAsync()
    {
        // Create and start PostgreSQL container with TimescaleDB extension
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("telemetry_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        // Create DbContext
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        _context = new TelemetryDbContext(options);

        // Apply migrations
        await _context.Database.MigrateAsync();

        // Create repository
        _repository = new DeviceMetricsRepository(_context);
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
    public async Task AddAsync_ShouldPersistDeviceMetrics()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var timestamp = DateTimeOffset.UtcNow;
        var metrics = DeviceMetrics.Create(
            deviceId,
            timestamp,
            cpuUsage: 45.5,
            memoryUsage: 60.2,
            diskUsage: 70.8,
            uptimeSeconds: 3600,
            runningContainers: 3
        );

        // Act
        await _repository!.AddAsync(metrics);
        await _repository.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetLatestByDeviceAsync(deviceId);
        retrieved.Should().NotBeNull();
        retrieved!.DeviceId.Should().Be(deviceId);
        retrieved.CpuUsage.Should().Be(45.5);
        retrieved.MemoryUsage.Should().Be(60.2);
        retrieved.DiskUsage.Should().Be(70.8);
        retrieved.UptimeSeconds.Should().Be(3600);
        retrieved.RunningContainers.Should().Be(3);
    }

    [Fact]
    public async Task AddRangeAsync_ShouldPersistMultipleMetrics()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);
        var metricsList = new List<DeviceMetrics>();

        for (int i = 0; i < 10; i++)
        {
            metricsList.Add(DeviceMetrics.Create(
                deviceId,
                baseTime.AddMinutes(i * 30),
                cpuUsage: 40 + i,
                memoryUsage: 50 + i,
                diskUsage: 60 + i,
                uptimeSeconds: 3600 + (i * 1800),
                runningContainers: 3
            ));
        }

        // Act
        await _repository!.AddRangeAsync(metricsList);
        await _repository.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByDeviceAndTimeRangeAsync(
            deviceId,
            baseTime.AddHours(-1),
            baseTime.AddHours(10)
        );

        retrieved.Should().HaveCount(10);
        retrieved.Should().BeInDescendingOrder(m => m.Timestamp);
    }

    [Fact]
    public async Task GetByDeviceAndTimeRangeAsync_ShouldReturnMetricsInRange()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);

        var metricsInRange = new List<DeviceMetrics>
        {
            DeviceMetrics.Create(deviceId, baseTime.AddHours(1), 40, 50, 60, 3600, 3),
            DeviceMetrics.Create(deviceId, baseTime.AddHours(2), 45, 55, 65, 7200, 3),
            DeviceMetrics.Create(deviceId, baseTime.AddHours(3), 50, 60, 70, 10800, 3)
        };

        var metricsOutOfRange = new List<DeviceMetrics>
        {
            DeviceMetrics.Create(deviceId, baseTime.AddHours(-5), 35, 45, 55, 1800, 3),
            DeviceMetrics.Create(deviceId, baseTime.AddHours(10), 55, 65, 75, 14400, 3)
        };

        await _repository!.AddRangeAsync(metricsInRange.Concat(metricsOutOfRange));
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDeviceAndTimeRangeAsync(
            deviceId,
            baseTime,
            baseTime.AddHours(5)
        );

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(m => m.Timestamp >= baseTime && m.Timestamp <= baseTime.AddHours(5));
    }

    [Fact]
    public async Task GetLatestByDeviceAsync_ShouldReturnMostRecentMetrics()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);

        var metrics = new List<DeviceMetrics>
        {
            DeviceMetrics.Create(deviceId, baseTime.AddHours(1), 40, 50, 60, 3600, 3),
            DeviceMetrics.Create(deviceId, baseTime.AddHours(2), 45, 55, 65, 7200, 3),
            DeviceMetrics.Create(deviceId, baseTime.AddHours(3), 50, 60, 70, 10800, 3)
        };

        await _repository!.AddRangeAsync(metrics);
        await _repository.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestByDeviceAsync(deviceId);

        // Assert
        latest.Should().NotBeNull();
        latest!.CpuUsage.Should().Be(50);
        latest.Timestamp.Should().BeCloseTo(baseTime.AddHours(3), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetLatestByDeviceAsync_ShouldReturnNullWhenNoMetrics()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());

        // Act
        var latest = await _repository!.GetLatestByDeviceAsync(deviceId);

        // Assert
        latest.Should().BeNull();
    }

    [Fact]
    public async Task GetHourlyAggregatesAsync_ShouldReturnAggregatedData()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow.AddDays(-2);

        // Create metrics spread across several hours
        var metrics = new List<DeviceMetrics>();
        for (int hour = 0; hour < 24; hour++)
        {
            for (int minute = 0; minute < 60; minute += 15)
            {
                metrics.Add(DeviceMetrics.Create(
                    deviceId,
                    baseTime.AddHours(hour).AddMinutes(minute),
                    cpuUsage: 40 + (hour % 10),
                    memoryUsage: 50 + (hour % 10),
                    diskUsage: 60 + (hour % 10),
                    uptimeSeconds: 3600 * hour,
                    runningContainers: 3
                ));
            }
        }

        await _repository!.AddRangeAsync(metrics);
        await _repository.SaveChangesAsync();

        // Wait for continuous aggregate to refresh (in real scenario)
        // For tests, we might need to manually refresh or wait
        await Task.Delay(1000);

        // Act
        var aggregates = await _repository.GetHourlyAggregatesAsync(
            deviceId,
            baseTime.AddHours(-1),
            baseTime.AddHours(25)
        );

        // Assert
        aggregates.Should().NotBeEmpty();
        // Note: Continuous aggregates might not be immediately available in test environment
        // This test verifies the query works, not the TimescaleDB automation
    }
}
