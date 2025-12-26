using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to rollback a rollout to the previous version.
/// </summary>
public record RollbackRolloutCommand(
    Guid RolloutId);

/// <summary>
/// Response after rolling back a rollout.
/// </summary>
public record RollbackRolloutResponse(
    Guid RolloutId,
    string PreviousVersion,
    DateTimeOffset RolledBackAt,
    int DevicesAffected);

/// <summary>
/// Handler for RollbackRolloutCommand.
/// </summary>
public class RollbackRolloutHandler
{
    private readonly IRolloutRepository _rolloutRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IDeviceGroupRepository _deviceGroupRepository;

    public RollbackRolloutHandler(
        IRolloutRepository rolloutRepository,
        IDeviceDesiredStateRepository desiredStateRepository,
        IDeviceGroupRepository deviceGroupRepository)
    {
        _rolloutRepository = rolloutRepository;
        _desiredStateRepository = desiredStateRepository;
        _deviceGroupRepository = deviceGroupRepository;
    }

    public async Task<Result<RollbackRolloutResponse>> Handle(
        RollbackRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout with all phases and device assignments
        var rollout = await _rolloutRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<RollbackRolloutResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        // 2. Rollback rollout (domain logic validates status and previous version)
        try
        {
            var rolledBackAt = DateTimeOffset.UtcNow;
            rollout.Rollback(rolledBackAt);

            if (rollout.PreviousVersion is null)
            {
                return Result.Failure<RollbackRolloutResponse>(
                    Error.Validation("NO_PREVIOUS_VERSION", "No previous version specified for rollback."));
            }

            // 3. Get all devices that were assigned in this rollout
            var affectedDeviceIds = new HashSet<DeviceId>();
            foreach (var phase in rollout.Phases)
            {
                foreach (var assignment in phase.DeviceAssignments)
                {
                    affectedDeviceIds.Add(assignment.DeviceId);
                }
            }

            // 4. Update desired state for all affected devices to previous version
            foreach (var deviceId in affectedDeviceIds)
            {
                var existingDesiredState = await _desiredStateRepository.GetByDeviceIdAsync(
                    deviceId, cancellationToken);

                if (existingDesiredState is not null)
                {
                    existingDesiredState.UpdateBundleVersion(
                        rollout.PreviousVersion,
                        $"Rollback from: {rollout.Name}",
                        rolledBackAt);
                    await _desiredStateRepository.UpdateAsync(existingDesiredState, cancellationToken);
                }
            }

            // 5. Save changes
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            await _desiredStateRepository.SaveChangesAsync(cancellationToken);
            await _rolloutRepository.SaveChangesAsync(cancellationToken);

            // 6. Return response
            return Result<RollbackRolloutResponse>.Success(new RollbackRolloutResponse(
                rollout.Id,
                rollout.PreviousVersion.ToString(),
                rolledBackAt,
                affectedDeviceIds.Count));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RollbackRolloutResponse>(
                Error.Validation("INVALID_OPERATION", ex.Message));
        }
    }
}
