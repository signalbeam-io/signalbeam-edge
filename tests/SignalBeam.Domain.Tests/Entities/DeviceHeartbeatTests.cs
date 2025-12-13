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
}
