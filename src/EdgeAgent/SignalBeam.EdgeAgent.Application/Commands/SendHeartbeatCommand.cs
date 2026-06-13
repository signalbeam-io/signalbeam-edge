using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record SendHeartbeatCommand(Guid DeviceId);

public class SendHeartbeatCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly IHeartbeatPublisher _heartbeatPublisher;
    private readonly IMetricsPublisher _metricsPublisher;
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<SendHeartbeatCommandHandler> _logger;

    public SendHeartbeatCommandHandler(
        ICloudClient cloudClient,
        IHeartbeatPublisher heartbeatPublisher,
        IMetricsPublisher metricsPublisher,
        IMetricsCollector metricsCollector,
        ILogger<SendHeartbeatCommandHandler> logger)
    {
        _cloudClient = cloudClient;
        _heartbeatPublisher = heartbeatPublisher;
        _metricsPublisher = metricsPublisher;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<Result> Handle(
        SendHeartbeatCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _metricsCollector.CollectMetricsAsync(cancellationToken);

            var heartbeat = new DeviceHeartbeat(
                command.DeviceId,
                DateTime.UtcNow,
                metrics);

            // Publish host metrics to the telemetry pipeline (JetStream). Best-effort:
            // a NATS outage must not fail the heartbeat — the metrics also ride on the
            // HTTP heartbeat below, which DeviceManager persists for the device status view.
            await TryPublishMetricsViaNatsAsync(command.DeviceId, metrics, cancellationToken);

            // Dual-publish heartbeat status: try NATS first, fall back to HTTP
            var natsSucceeded = await TryPublishViaNatsAsync(command.DeviceId, cancellationToken);

            if (!natsSucceeded)
            {
                _logger.LogDebug(
                    "NATS heartbeat failed for device {DeviceId}, falling back to HTTP",
                    command.DeviceId);
            }

            // Always send via HTTP to maintain compatibility during migration
            await _cloudClient.SendHeartbeatAsync(heartbeat, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                Error.Failure("Heartbeat.Failed", $"Failed to send heartbeat: {ex.Message}"));
        }
    }

    private async Task TryPublishMetricsViaNatsAsync(
        Guid deviceId,
        DeviceMetrics metrics,
        CancellationToken cancellationToken)
    {
        try
        {
            await _metricsPublisher.PublishMetricsAsync(
                deviceId,
                metrics,
                metrics.RunningContainers,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish metrics via NATS for device {DeviceId}",
                deviceId);
        }
    }

    private async Task<bool> TryPublishViaNatsAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _heartbeatPublisher.PublishHeartbeatAsync(
                deviceId,
                "online",
                ipAddress: null,
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish heartbeat via NATS for device {DeviceId}",
                deviceId);

            return false;
        }
    }
}
