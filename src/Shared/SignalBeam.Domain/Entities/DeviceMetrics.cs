using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents detailed telemetry/metrics data from a device.
/// Time-series data stored in TimescaleDB.
/// </summary>
public class DeviceMetrics : Entity<Guid>
{
    /// <summary>
    /// Device this metrics data belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// When the metrics were collected (UTC).
    /// Used as hypertable partition key in TimescaleDB.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Metric name/key (e.g., "container.cpu.usage", "network.rx.bytes").
    /// </summary>
    public string MetricName { get; private set; } = string.Empty;

    /// <summary>
    /// Metric value.
    /// </summary>
    public double Value { get; private set; }

    /// <summary>
    /// Unit of measurement (e.g., "percent", "bytes", "celsius").
    /// </summary>
    public string? Unit { get; private set; }

    /// <summary>
    /// Tags/dimensions for the metric as JSON (e.g., {"container":"nginx","interface":"eth0"}).
    /// </summary>
    public string? TagsJson { get; private set; }

    // EF Core constructor
    private DeviceMetrics() : base()
    {
    }

    private DeviceMetrics(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string metricName,
        double value) : base(id)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            throw new ArgumentException("Metric name cannot be empty.", nameof(metricName));

        DeviceId = deviceId;
        Timestamp = timestamp;
        MetricName = metricName;
        Value = value;
    }

    /// <summary>
    /// Factory method to create a device metric.
    /// </summary>
    public static DeviceMetrics Create(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        string metricName,
        double value,
        string? unit = null,
        string? tagsJson = null)
    {
        return new DeviceMetrics(Guid.NewGuid(), deviceId, timestamp, metricName, value)
        {
            Unit = unit,
            TagsJson = tagsJson
        };
    }
}
