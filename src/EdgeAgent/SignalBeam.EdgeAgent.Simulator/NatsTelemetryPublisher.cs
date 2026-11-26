using System.Diagnostics;
using System.Text.Json;
using NATS.Client.Core;

namespace SignalBeam.EdgeAgent.Simulator;

internal sealed record NatsHeartbeatMessage(Guid DeviceId, DateTimeOffset Timestamp, string Status);

internal sealed record NatsMetricsMessage(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers);

// Publishes heartbeats and metrics the way the real edge agent does, so TelemetryProcessor
// (stale-device detection, health scores, alert rules) sees the simulated fleet. The subjects
// are captured by the DEVICE_HEARTBEATS / DEVICE_METRICS JetStream streams. NATS.Net adds the
// traceparent header, so the consumer continues the simulator's trace.
internal sealed class NatsTelemetryPublisher : IAsyncDisposable
{
    private readonly NatsConnection _connection;

    public NatsTelemetryPublisher(string url)
    {
        _connection = new NatsConnection(new NatsOpts { Url = NormalizeUrl(url), Name = "signalbeam-simulator" });
    }

    public Task PublishHeartbeatAsync(Guid deviceId, string status, CancellationToken cancellationToken) =>
        PublishAsync(
            $"signalbeam.devices.heartbeat.{deviceId}",
            new NatsHeartbeatMessage(deviceId, DateTimeOffset.UtcNow, status),
            cancellationToken);

    public Task PublishMetricsAsync(NatsMetricsMessage metrics, CancellationToken cancellationToken) =>
        PublishAsync($"signalbeam.telemetry.metrics.{metrics.DeviceId}", metrics, cancellationToken);

    private async Task PublishAsync<T>(string subject, T message, CancellationToken cancellationToken)
    {
        try
        {
            // PascalCase, matching what TelemetryProcessor deserializes.
            await _connection.PublishAsync(subject, JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // NATS being down mustn't stop the HTTP side of the tick; the span records the failure.
            Activity.Current?.SetStatus(ActivityStatusCode.Error, $"nats publish failed: {ex.GetType().Name}");
            Activity.Current?.AddException(ex);
        }
    }

    // Aspire hands out endpoints as tcp://host:port.
    internal static string NormalizeUrl(string url) =>
        url.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase) ? "nats://" + url["tcp://".Length..]
        : url.Contains("://", StringComparison.Ordinal) ? url
        : "nats://" + url;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
