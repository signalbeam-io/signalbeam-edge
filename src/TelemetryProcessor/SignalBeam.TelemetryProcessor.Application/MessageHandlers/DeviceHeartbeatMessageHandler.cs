using Microsoft.Extensions.Logging;
using SignalBeam.TelemetryProcessor.Application.Commands;

namespace SignalBeam.TelemetryProcessor.Application.MessageHandlers;

/// <summary>
/// Handles DeviceHeartbeatMessage from NATS.
/// Processes heartbeats and updates device status.
/// </summary>
public class DeviceHeartbeatMessageHandler
{
    private readonly ProcessHeartbeatHandler _processHeartbeatHandler;
    private readonly ILogger<DeviceHeartbeatMessageHandler> _logger;

    public DeviceHeartbeatMessageHandler(
        ProcessHeartbeatHandler processHeartbeatHandler,
        ILogger<DeviceHeartbeatMessageHandler> logger)
    {
        _processHeartbeatHandler = processHeartbeatHandler;
        _logger = logger;
    }

    /// <summary>
    /// Handles incoming heartbeat message from NATS.
    /// Wolverine will automatically discover this handler.
    /// </summary>
    public async Task Handle(DeviceHeartbeatMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing heartbeat for device {DeviceId} at {Timestamp}",
            message.DeviceId,
            message.Timestamp);

        try
        {
            var command = new ProcessHeartbeatCommand(
                message.DeviceId,
                message.Timestamp,
                message.Status,
                message.IpAddress,
                message.AdditionalData);

            var result = await _processHeartbeatHandler.Handle(command, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to process heartbeat for device {DeviceId}: {ErrorCode} - {ErrorMessage}",
                    message.DeviceId,
                    result.Error?.Code,
                    result.Error?.Message);
            }
            else
            {
                _logger.LogDebug(
                    "Successfully processed heartbeat for device {DeviceId}, status: {Status}",
                    message.DeviceId,
                    result.Value?.DeviceStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing heartbeat for device {DeviceId}",
                message.DeviceId);
            throw; // Let Wolverine handle retries
        }
    }
}
