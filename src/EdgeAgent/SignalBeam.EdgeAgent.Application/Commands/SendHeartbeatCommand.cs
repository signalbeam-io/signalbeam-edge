using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record SendHeartbeatCommand(Guid DeviceId);

public class SendHeartbeatCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly IMetricsCollector _metricsCollector;

    public SendHeartbeatCommandHandler(
        ICloudClient cloudClient,
        IMetricsCollector metricsCollector)
    {
        _cloudClient = cloudClient;
        _metricsCollector = metricsCollector;
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

            await _cloudClient.SendHeartbeatAsync(heartbeat, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                Error.Failure("Heartbeat.Failed", $"Failed to send heartbeat: {ex.Message}"));
        }
    }
}
