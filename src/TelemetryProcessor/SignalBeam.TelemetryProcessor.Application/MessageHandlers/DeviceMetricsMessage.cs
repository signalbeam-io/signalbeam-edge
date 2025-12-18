namespace SignalBeam.TelemetryProcessor.Application.MessageHandlers;

/// <summary>
/// Message received from NATS when a device reports metrics.
/// Subject: signalbeam.telemetry.metrics.{deviceId}
/// </summary>
public record DeviceMetricsMessage(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics = null);
