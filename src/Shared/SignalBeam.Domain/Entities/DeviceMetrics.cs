using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents device metrics captured at a specific point in time.
/// Stored in TimescaleDB for time-series queries.
/// </summary>
public class DeviceMetrics : Entity<Guid>
{
    /// <summary>
    /// Device that reported these metrics.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Timestamp when metrics were captured (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsage { get; private set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsage { get; private set; }

    /// <summary>
    /// Disk usage percentage (0-100).
    /// </summary>
    public double DiskUsage { get; private set; }

    /// <summary>
    /// Device uptime in seconds.
    /// </summary>
    public long UptimeSeconds { get; private set; }

    /// <summary>
    /// Number of running containers.
    /// </summary>
    public int RunningContainers { get; private set; }

    /// <summary>
    /// Additional metrics as JSON (optional).
    /// </summary>
    public string? AdditionalMetrics { get; private set; }

    // EF Core constructor
    private DeviceMetrics() : base()
    {
    }

    private DeviceMetrics(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp,
        double cpuUsage,
        double memoryUsage,
        double diskUsage,
        long uptimeSeconds,
        int runningContainers,
        string? additionalMetrics = null) : base(id)
    {
        DeviceId = deviceId;
        Timestamp = timestamp;
        CpuUsage = cpuUsage;
        MemoryUsage = memoryUsage;
        DiskUsage = diskUsage;
        UptimeSeconds = uptimeSeconds;
        RunningContainers = runningContainers;
        AdditionalMetrics = additionalMetrics;
    }

    /// <summary>
    /// Factory method to create device metrics.
    /// </summary>
    public static DeviceMetrics Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        double cpuUsage,
        double memoryUsage,
        double diskUsage,
        long uptimeSeconds,
        int runningContainers,
        string? additionalMetrics = null)
    {
        ValidateMetrics(cpuUsage, memoryUsage, diskUsage);

        return new DeviceMetrics(
            Guid.NewGuid(),
            deviceId,
            timestamp,
            cpuUsage,
            memoryUsage,
            diskUsage,
            uptimeSeconds,
            runningContainers,
            additionalMetrics);
    }

    private static void ValidateMetrics(double cpuUsage, double memoryUsage, double diskUsage)
    {
        if (cpuUsage < 0 || cpuUsage > 100)
            throw new ArgumentException("CPU usage must be between 0 and 100.", nameof(cpuUsage));

        if (memoryUsage < 0 || memoryUsage > 100)
            throw new ArgumentException("Memory usage must be between 0 and 100.", nameof(memoryUsage));

        if (diskUsage < 0 || diskUsage > 100)
            throw new ArgumentException("Disk usage must be between 0 and 100.", nameof(diskUsage));
    }
}
