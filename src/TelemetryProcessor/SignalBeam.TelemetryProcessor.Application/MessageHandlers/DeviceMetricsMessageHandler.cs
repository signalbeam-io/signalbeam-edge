using Microsoft.Extensions.Logging;
using SignalBeam.TelemetryProcessor.Application.Commands;

namespace SignalBeam.TelemetryProcessor.Application.MessageHandlers;

/// <summary>
/// Handles DeviceMetricsMessage from NATS.
/// Processes device metrics and stores in TimescaleDB.
/// </summary>
public class DeviceMetricsMessageHandler
{
    private readonly ProcessMetricsHandler _processMetricsHandler;
    private readonly ILogger<DeviceMetricsMessageHandler> _logger;

    public DeviceMetricsMessageHandler(
        ProcessMetricsHandler processMetricsHandler,
        ILogger<DeviceMetricsMessageHandler> logger)
    {
        _processMetricsHandler = processMetricsHandler;
        _logger = logger;
    }

    /// <summary>
    /// Handles incoming metrics message from NATS.
    /// Wolverine will automatically discover this handler.
    /// </summary>
    public async Task Handle(DeviceMetricsMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing metrics for device {DeviceId} at {Timestamp}: CPU={CpuUsage}%, Memory={MemoryUsage}%, Disk={DiskUsage}%",
            message.DeviceId,
            message.Timestamp,
            message.CpuUsage,
            message.MemoryUsage,
            message.DiskUsage);

        try
        {
            var command = new ProcessMetricsCommand(
                message.DeviceId,
                message.Timestamp,
                message.CpuUsage,
                message.MemoryUsage,
                message.DiskUsage,
                message.UptimeSeconds,
                message.RunningContainers,
                message.AdditionalMetrics);

            var result = await _processMetricsHandler.Handle(command, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to process metrics for device {DeviceId}: {ErrorCode} - {ErrorMessage}",
                    message.DeviceId,
                    result.Error?.Code,
                    result.Error?.Message);
            }
            else
            {
                _logger.LogDebug(
                    "Successfully processed metrics for device {DeviceId}, metrics ID: {MetricsId}",
                    message.DeviceId,
                    result.Value?.MetricsId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing metrics for device {DeviceId}",
                message.DeviceId);
            throw; // Let Wolverine handle retries
        }
    }
}
