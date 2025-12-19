using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record ReconcileContainersCommand(
    Guid DeviceId,
    DesiredState? DesiredState);

public class ReconcileContainersCommandHandler
{
    private readonly IContainerManager _containerManager;

    public ReconcileContainersCommandHandler(IContainerManager containerManager)
    {
        _containerManager = containerManager;
    }

    public async Task<Result<ReconciliationResult>> Handle(
        ReconcileContainersCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var runningContainers = await _containerManager.GetRunningContainersAsync(cancellationToken);

            if (command.DesiredState == null || command.DesiredState.Containers.Count == 0)
            {
                // No desired state - stop all containers
                await StopAllContainersAsync(runningContainers, cancellationToken);
                return Result<ReconciliationResult>.Success(
                    new ReconciliationResult(0, runningContainers.Count, 0));
            }

            var desiredContainers = command.DesiredState.Containers;
            var reconciliationResult = await ReconcileAsync(
                runningContainers,
                desiredContainers,
                cancellationToken);

            return Result<ReconciliationResult>.Success(reconciliationResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<ReconciliationResult>(
                Error.Failure("Reconciliation.Failed", $"Failed to reconcile containers: {ex.Message}"));
        }
    }

    private async Task<ReconciliationResult> ReconcileAsync(
        List<ContainerStatus> runningContainers,
        List<ContainerSpec> desiredContainers,
        CancellationToken cancellationToken)
    {
        int started = 0;
        int stopped = 0;
        int failed = 0;

        // Build a map of desired containers by name
        var desiredMap = desiredContainers.ToDictionary(c => c.Name);
        var runningMap = runningContainers.ToDictionary(c => c.Name);

        // Stop containers that are not in desired state
        foreach (var running in runningContainers)
        {
            if (!desiredMap.ContainsKey(running.Name))
            {
                try
                {
                    await _containerManager.StopContainerAsync(running.Id, cancellationToken);
                    stopped++;
                }
                catch
                {
                    failed++;
                }
            }
        }

        // Start containers that are in desired state but not running
        foreach (var desired in desiredContainers)
        {
            if (!runningMap.ContainsKey(desired.Name))
            {
                try
                {
                    // Pull image first
                    await _containerManager.PullImageAsync(desired.Image, null, cancellationToken);

                    // Start container
                    await _containerManager.StartContainerAsync(desired, cancellationToken);
                    started++;
                }
                catch
                {
                    failed++;
                }
            }
            else
            {
                // Check if the image has changed
                var running = runningMap[desired.Name];
                if (running.Image != desired.Image)
                {
                    try
                    {
                        // Stop old container
                        await _containerManager.StopContainerAsync(running.Id, cancellationToken);

                        // Pull new image
                        await _containerManager.PullImageAsync(desired.Image, null, cancellationToken);

                        // Start new container
                        await _containerManager.StartContainerAsync(desired, cancellationToken);

                        stopped++;
                        started++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
        }

        return new ReconciliationResult(started, stopped, failed);
    }

    private async Task StopAllContainersAsync(
        List<ContainerStatus> runningContainers,
        CancellationToken cancellationToken)
    {
        foreach (var container in runningContainers)
        {
            try
            {
                await _containerManager.StopContainerAsync(container.Id, cancellationToken);
            }
            catch
            {
                // Log but continue
            }
        }
    }
}

public record ReconciliationResult(
    int ContainersStarted,
    int ContainersStopped,
    int ContainersFailed);
