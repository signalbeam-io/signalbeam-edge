using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceHeartbeatTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateHeartbeat()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            cpuUsagePercent: 45.5,
            memoryUsagePercent: 60.2,
            diskUsagePercent: 30.0,
            temperatureCelsius: 55.0,
            runningContainersCount: 3,
            uptimeSeconds: 3600);

        // Assert
        Assert.Equal(deviceId, heartbeat.DeviceId);
        Assert.Equal(timestamp, heartbeat.Timestamp);
        Assert.Equal(45.5, heartbeat.CpuUsagePercent);
        Assert.Equal(60.2, heartbeat.MemoryUsagePercent);
        Assert.Equal(30.0, heartbeat.DiskUsagePercent);
        Assert.Equal(55.0, heartbeat.TemperatureCelsius);
        Assert.Equal(3, heartbeat.RunningContainersCount);
        Assert.Equal(3600, heartbeat.UptimeSeconds);
    }

    [Fact]
    public void Create_WithMinimalData_ShouldCreateHeartbeat()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(deviceId, timestamp);

        // Assert
        Assert.Equal(deviceId, heartbeat.DeviceId);
        Assert.Equal(timestamp, heartbeat.Timestamp);
        Assert.Null(heartbeat.CpuUsagePercent);
        Assert.Null(heartbeat.MemoryUsagePercent);
    }

    [Fact]
    public void Create_WithAllMetrics_ShouldStoreAllValues()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var additionalMetrics = "{\"network_rx\":1024000,\"network_tx\":512000}";

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            cpuUsagePercent: 75.5,
            memoryUsagePercent: 82.3,
            diskUsagePercent: 45.8,
            temperatureCelsius: 68.5,
            runningContainersCount: 5,
            uptimeSeconds: 86400,
            additionalMetricsJson: additionalMetrics);

        // Assert
        Assert.Equal(75.5, heartbeat.CpuUsagePercent);
        Assert.Equal(82.3, heartbeat.MemoryUsagePercent);
        Assert.Equal(45.8, heartbeat.DiskUsagePercent);
        Assert.Equal(68.5, heartbeat.TemperatureCelsius);
        Assert.Equal(5, heartbeat.RunningContainersCount);
        Assert.Equal(86400, heartbeat.UptimeSeconds);
        Assert.Equal(additionalMetrics, heartbeat.AdditionalMetricsJson);
    }

    [Fact]
    public void Create_WithZeroValues_ShouldStoreZeros()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            cpuUsagePercent: 0,
            memoryUsagePercent: 0,
            diskUsagePercent: 0,
            runningContainersCount: 0,
            uptimeSeconds: 0);

        // Assert
        Assert.Equal(0, heartbeat.CpuUsagePercent);
        Assert.Equal(0, heartbeat.MemoryUsagePercent);
        Assert.Equal(0, heartbeat.DiskUsagePercent);
        Assert.Equal(0, heartbeat.RunningContainersCount);
        Assert.Equal(0, heartbeat.UptimeSeconds);
    }

    [Fact]
    public void Create_WithHighCpuUsage_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            cpuUsagePercent: 99.9);

        // Assert
        Assert.Equal(99.9, heartbeat.CpuUsagePercent);
    }

    [Fact]
    public void Create_WithHighMemoryUsage_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            memoryUsagePercent: 95.5);

        // Assert
        Assert.Equal(95.5, heartbeat.MemoryUsagePercent);
    }

    [Fact]
    public void Create_WithLongUptime_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var thirtyDaysInSeconds = 30L * 24 * 60 * 60;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            uptimeSeconds: thirtyDaysInSeconds);

        // Assert
        Assert.Equal(thirtyDaysInSeconds, heartbeat.UptimeSeconds);
    }

    [Fact]
    public void Create_WithAdditionalMetrics_ShouldStoreJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var metrics = "{\"custom_metric\":123.45,\"another_metric\":\"value\"}";

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            additionalMetricsJson: metrics);

        // Assert
        Assert.Equal(metrics, heartbeat.AdditionalMetricsJson);
    }

    [Fact]
    public void Create_MultipleHeartbeatsForDevice_ShouldHaveDifferentIds()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp1 = DateTimeOffset.UtcNow;
        var timestamp2 = timestamp1.AddSeconds(30);
        var timestamp3 = timestamp1.AddMinutes(1);

        // Act
        var heartbeat1 = DeviceHeartbeat.Create(deviceId, timestamp1);
        var heartbeat2 = DeviceHeartbeat.Create(deviceId, timestamp2);
        var heartbeat3 = DeviceHeartbeat.Create(deviceId, timestamp3);

        // Assert
        Assert.NotEqual(heartbeat1.Id, heartbeat2.Id);
        Assert.NotEqual(heartbeat2.Id, heartbeat3.Id);
        Assert.Equal(deviceId, heartbeat1.DeviceId);
        Assert.Equal(deviceId, heartbeat2.DeviceId);
        Assert.Equal(deviceId, heartbeat3.DeviceId);
    }

    [Fact]
    public void Create_WithNegativeTemperature_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            temperatureCelsius: -10.5);

        // Assert
        Assert.Equal(-10.5, heartbeat.TemperatureCelsius);
    }

    [Fact]
    public void Create_WithManyContainers_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            timestamp,
            runningContainersCount: 50);

        // Assert
        Assert.Equal(50, heartbeat.RunningContainersCount);
    }
}
