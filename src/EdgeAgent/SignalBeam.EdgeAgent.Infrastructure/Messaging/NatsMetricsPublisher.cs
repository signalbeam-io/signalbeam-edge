using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Infrastructure.Messaging;

public sealed class NatsMetricsPublisher : IMetricsPublisher
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<NatsMetricsPublisher> _logger;

    public NatsMetricsPublisher(
        IMessagePublisher publisher,
        ILogger<NatsMetricsPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishMetricsAsync(
        Guid deviceId,
        DeviceMetrics metrics,
        int runningContainers,
        CancellationToken cancellationToken = default)
    {
        var subject = $"signalbeam.telemetry.metrics.{deviceId}";

        var message = new DeviceMetricsMessage(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow,
            CpuUsage: metrics.CpuUsagePercent,
            MemoryUsage: metrics.MemoryUsagePercent,
            DiskUsage: metrics.DiskUsagePercent,
            UptimeSeconds: metrics.UptimeSeconds,
            RunningContainers: runningContainers);

        _logger.LogDebug(
            "Publishing metrics for device {DeviceId} to {Subject}",
            deviceId, subject);

        await _publisher.PublishAsync(subject, message, cancellationToken);
    }
}

/// <summary>
/// Message schema matching TelemetryProcessor's DeviceMetricsMessage.
/// Published to JetStream stream DEVICE_METRICS.
/// </summary>
internal record DeviceMetricsMessage(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics = null);
