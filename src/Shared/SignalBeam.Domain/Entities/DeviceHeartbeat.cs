using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a heartbeat from a device with basic health metrics.
/// Time-series data stored in TimescaleDB.
/// </summary>
public class DeviceHeartbeat : Entity<Guid>
{
    /// <summary>
    /// Device this heartbeat belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// When the heartbeat was received (UTC).
    /// Used as hypertable partition key in TimescaleDB.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double? CpuUsagePercent { get; private set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double? MemoryUsagePercent { get; private set; }

    /// <summary>
    /// Disk usage percentage (0-100).
    /// </summary>
    public double? DiskUsagePercent { get; private set; }

    /// <summary>
    /// Device temperature in Celsius (optional, for devices with sensors).
    /// </summary>
    public double? TemperatureCelsius { get; private set; }

    /// <summary>
    /// Number of running containers.
    /// </summary>
    public int? RunningContainersCount { get; private set; }

    /// <summary>
    /// Device uptime in seconds.
    /// </summary>
    public long? UptimeSeconds { get; private set; }

    /// <summary>
    /// Additional metrics as JSON.
    /// </summary>
    public string? AdditionalMetricsJson { get; private set; }

    // EF Core constructor
    private DeviceHeartbeat() : base()
    {
    }

    private DeviceHeartbeat(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp) : base(id)
    {
        DeviceId = deviceId;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Factory method to create a heartbeat.
    /// </summary>
    public static DeviceHeartbeat Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        double? cpuUsagePercent = null,
        double? memoryUsagePercent = null,
        double? diskUsagePercent = null,
        double? temperatureCelsius = null,
        int? runningContainersCount = null,
        long? uptimeSeconds = null,
        string? additionalMetricsJson = null)
    {
        return new DeviceHeartbeat(Guid.NewGuid(), deviceId, timestamp)
        {
            CpuUsagePercent = cpuUsagePercent,
            MemoryUsagePercent = memoryUsagePercent,
            DiskUsagePercent = diskUsagePercent,
            TemperatureCelsius = temperatureCelsius,
            RunningContainersCount = runningContainersCount,
            UptimeSeconds = uptimeSeconds,
            AdditionalMetricsJson = additionalMetricsJson
        };
    }
}
