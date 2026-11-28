using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.EdgeAgent.Application.Commands;

public record ReconcileContainersCommand(
    Guid DeviceId,
    DesiredState? DesiredState);

public class ReconcileContainersCommandHandler
{
    private readonly IContainerManager _containerManager;
    private readonly ILogger<ReconcileContainersCommandHandler> _logger;

    public ReconcileContainersCommandHandler(
        IContainerManager containerManager,
        ILogger<ReconcileContainersCommandHandler> logger)
    {
        _containerManager = containerManager;
        _logger = logger;
    }

    public async Task<Result<ReconciliationResult>> Handle(
        ReconcileContainersCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting container reconciliation for device {DeviceId}", command.DeviceId);

            // No desired state (nothing assigned, or it couldn't be read): leave the device as it is.
            if (command.DesiredState == null)
            {
                _logger.LogInformation("No desired state for device {DeviceId}; leaving containers as they are", command.DeviceId);
                return Result<ReconciliationResult>.Success(new ReconciliationResult(0, 0, 0, [], []));
            }

            // Only containers the agent created; the agent's own container and anything else on the
            // host are never listed, so never stopped.
            var runningContainers = await _containerManager.GetManagedContainersAsync(cancellationToken);
            var owner = new ContainerOwner(command.DesiredState.BundleId, command.DesiredState.BundleVersion);

            // A desired state with no containers means "run nothing": stop the managed containers only.
            if (command.DesiredState.Containers is not { Count: > 0 })
            {
                _logger.LogInformation(
                    "Desired state has no containers - stopping all {Count} managed containers", runningContainers.Count);

                var (actions, errors) = await StopAllContainersAsync(runningContainers, cancellationToken);
                return Result<ReconciliationResult>.Success(
                    new ReconciliationResult(0, actions.Count, errors.Count, actions, errors));
            }

            var desiredContainers = command.DesiredState.Containers;
            _logger.LogInformation(
                "Reconciling {DesiredCount} desired containers with {RunningCount} running containers",
                desiredContainers.Count, runningContainers.Count);

            var reconciliationResult = await ReconcileAsync(
                runningContainers,
                desiredContainers,
                owner,
                cancellationToken);

            _logger.LogInformation(
                "Reconciliation complete: {Started} started, {Stopped} stopped, {Failed} failed",
                reconciliationResult.ContainersStarted,
                reconciliationResult.ContainersStopped,
                reconciliationResult.ContainersFailed);

            return Result<ReconciliationResult>.Success(reconciliationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile containers for device {DeviceId}", command.DeviceId);
            return Result.Failure<ReconciliationResult>(
                Error.Failure("Reconciliation.Failed", $"Failed to reconcile containers: {ex.Message}"));
        }
    }

    private async Task<ReconciliationResult> ReconcileAsync(
        List<ContainerStatus> runningContainers,
        List<ContainerSpec> desiredContainers,
        ContainerOwner owner,
        CancellationToken cancellationToken)
    {
        int started = 0;
        int stopped = 0;
        int failed = 0;
        var actions = new List<ReconciliationAction>();
        var errors = new List<string>();

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
                    _logger.LogInformation("Stopping unwanted container: {Name} ({Image})", running.Name, running.Image);
                    await _containerManager.StopContainerAsync(running.Id, cancellationToken);
                    stopped++;
                    actions.Add(new ReconciliationAction("stopped", running.Name, running.Image));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop container: {Name}", running.Name);
                    failed++;
                    errors.Add($"Failed to stop container {running.Name}: {ex.Message}");
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
                    _logger.LogInformation("Starting new container: {Name} ({Image})", desired.Name, desired.Image);

                    // Pull image first
                    await _containerManager.PullImageAsync(desired.Image, null, cancellationToken);

                    // Start container
                    await _containerManager.StartContainerAsync(desired, owner, cancellationToken);
                    started++;
                    actions.Add(new ReconciliationAction("started", desired.Name, desired.Image));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start container: {Name}", desired.Name);
                    failed++;
                    errors.Add($"Failed to start container {desired.Name}: {ex.Message}");
                }
            }
            else
            {
                // Check if the image has changed
                var running = runningMap[desired.Name];
                if ((running.ManagedImage ?? running.Image) != desired.Image)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Updating container {Name}: {OldImage} -> {NewImage}",
                            desired.Name, running.ManagedImage ?? running.Image, desired.Image);

                        // Stop old container
                        await _containerManager.StopContainerAsync(running.Id, cancellationToken);

                        // Pull new image
                        await _containerManager.PullImageAsync(desired.Image, null, cancellationToken);

                        // Start new container
                        await _containerManager.StartContainerAsync(desired, owner, cancellationToken);

                        stopped++;
                        started++;
                        actions.Add(new ReconciliationAction("updated", desired.Name, desired.Image));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update container: {Name}", desired.Name);
                        failed++;
                        errors.Add($"Failed to update container {desired.Name}: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogDebug("Container {Name} is already running with correct image: {Image}", desired.Name, desired.Image);
                }
            }
        }

        return new ReconciliationResult(started, stopped, failed, actions, errors);
    }

    private async Task<(List<ReconciliationAction> Actions, List<string> Errors)> StopAllContainersAsync(
        List<ContainerStatus> runningContainers,
        CancellationToken cancellationToken)
    {
        var actions = new List<ReconciliationAction>();
        var errors = new List<string>();

        foreach (var container in runningContainers)
        {
            try
            {
                _logger.LogInformation("Stopping container: {Name} ({Image})", container.Name, container.Image);
                await _containerManager.StopContainerAsync(container.Id, cancellationToken);
                actions.Add(new ReconciliationAction("stopped", container.Name, container.Image));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop container: {Name}", container.Name);
                errors.Add($"Failed to stop container {container.Name}: {ex.Message}");
            }
        }

        return (actions, errors);
    }
}

public record ReconciliationResult(
    int ContainersStarted,
    int ContainersStopped,
    int ContainersFailed,
    List<ReconciliationAction> Actions,
    List<string> Errors);

public record ReconciliationAction(
    string Action,
    string Container,
    string Image);
