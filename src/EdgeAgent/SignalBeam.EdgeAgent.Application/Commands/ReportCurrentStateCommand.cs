using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record ReportCurrentStateCommand(
    Guid DeviceId,
    string? CurrentBundleId,
    string? CurrentBundleVersion);

public class ReportCurrentStateCommandHandler
{
    private readonly ICloudClient _cloudClient;
    private readonly IContainerManager _containerManager;

    public ReportCurrentStateCommandHandler(
        ICloudClient cloudClient,
        IContainerManager containerManager)
    {
        _cloudClient = cloudClient;
        _containerManager = containerManager;
    }

    public async Task<Result> Handle(
        ReportCurrentStateCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var runningContainers = await _containerManager.GetRunningContainersAsync(cancellationToken);

            var currentState = new DeviceCurrentState(
                command.DeviceId,
                DateTime.UtcNow,
                command.CurrentBundleId,
                command.CurrentBundleVersion,
                runningContainers);

            await _cloudClient.ReportCurrentStateAsync(currentState, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                Error.Failure("ReportState.Failed", $"Failed to report current state: {ex.Message}"));
        }
    }
}
