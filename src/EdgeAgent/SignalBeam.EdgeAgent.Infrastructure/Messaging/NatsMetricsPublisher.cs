using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Messaging;

public sealed class NatsMetricsPublisher : IMetricsPublisher
{
    private readonly INatsJSContext _jetStream;
    private readonly ILogger<NatsMetricsPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public NatsMetricsPublisher(
        INatsJSContext jetStream,
        ILogger<NatsMetricsPublisher> logger)
    {
        _jetStream = jetStream;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task PublishMetricsAsync(
        Guid deviceId,
        DeviceMetrics metrics,
        int runningContainers,
        CancellationToken cancellationToken = default)
    {
        var subject = $"signalbeam.telemetry.metrics.{deviceId}";

        // Raw byte counters aren't first-class fields on the wire schema; carry them in
        // AdditionalMetrics so the telemetry pipeline can surface absolute memory/disk usage.
        var additionalMetrics = BuildAdditionalMetrics(metrics);

        var message = new DeviceMetricsMessage(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow,
            CpuUsage: metrics.CpuUsagePercent,
            MemoryUsage: metrics.MemoryUsagePercent,
            DiskUsage: metrics.DiskUsagePercent,
            UptimeSeconds: metrics.UptimeSeconds,
            RunningContainers: runningContainers,
            AdditionalMetrics: additionalMetrics);

        _logger.LogDebug(
            "Publishing metrics for device {DeviceId} to {Subject}",
            deviceId, subject);

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var ack = await _jetStream.PublishAsync(subject, json, cancellationToken: cancellationToken);
        ack.EnsureSuccess();
    }

    /// <summary>
    /// Serializes absolute byte counters as a compact JSON object, or returns null when no
    /// raw values are present (keeps the wire message lean for collectors that don't report them).
    /// </summary>
    private string? BuildAdditionalMetrics(DeviceMetrics metrics)
    {
        if (metrics.MemoryTotalBytes == 0 &&
            metrics.MemoryUsedBytes == 0 &&
            metrics.DiskTotalBytes == 0 &&
            metrics.DiskUsedBytes == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(
            new
            {
                memoryTotalBytes = metrics.MemoryTotalBytes,
                memoryUsedBytes = metrics.MemoryUsedBytes,
                diskTotalBytes = metrics.DiskTotalBytes,
                diskUsedBytes = metrics.DiskUsedBytes
            },
            _jsonOptions);
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
