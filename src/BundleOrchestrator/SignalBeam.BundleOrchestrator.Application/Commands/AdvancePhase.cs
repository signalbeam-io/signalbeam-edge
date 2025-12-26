using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to manually advance a rollout to the next phase.
/// </summary>
public record AdvancePhaseCommand(
    Guid RolloutId);

/// <summary>
/// Response after advancing a phase.
/// </summary>
public record AdvancePhaseResponse(
    Guid RolloutId,
    int NewPhaseNumber,
    string NewPhaseName,
    int TargetDeviceCount,
    DateTimeOffset AdvancedAt);

/// <summary>
/// Handler for AdvancePhaseCommand.
/// </summary>
public class AdvancePhaseHandler
{
    private readonly IRolloutRepository _rolloutRepository;
    private readonly IDeviceGroupRepository _deviceGroupRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;

    public AdvancePhaseHandler(
        IRolloutRepository rolloutRepository,
        IDeviceGroupRepository deviceGroupRepository,
        IDeviceDesiredStateRepository desiredStateRepository)
    {
        _rolloutRepository = rolloutRepository;
        _deviceGroupRepository = deviceGroupRepository;
        _desiredStateRepository = desiredStateRepository;
    }

    public async Task<Result<AdvancePhaseResponse>> Handle(
        AdvancePhaseCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout with all phases
        var rollout = await _rolloutRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<AdvancePhaseResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        // 2. Complete current phase and advance (domain logic validates status)
        try
        {
            var completedAt = DateTimeOffset.UtcNow;
            rollout.CompleteCurrentPhase(completedAt);

            // If we just completed the last phase, the rollout is now completed
            if (rollout.Status == Domain.Enums.RolloutLifecycleStatus.Completed)
            {
                await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
                await _rolloutRepository.SaveChangesAsync(cancellationToken);

                var lastPhase = rollout.GetCurrentPhase();
                return Result<AdvancePhaseResponse>.Success(new AdvancePhaseResponse(
                    rollout.Id,
                    rollout.CurrentPhaseNumber,
                    lastPhase?.Name ?? "Completed",
                    0,
                    completedAt));
            }

            // 3. Advance to next phase
            rollout.AdvancePhase();
            rollout.StartCurrentPhase(completedAt);

            var newPhase = rollout.GetCurrentPhase();
            if (newPhase is null)
            {
                return Result.Failure<AdvancePhaseResponse>(
                    Error.Validation("NO_NEXT_PHASE", "No next phase available."));
            }

            // 4. Get devices for the target group
            IReadOnlyList<DeviceId> targetDevices = new List<DeviceId>();
            if (rollout.TargetDeviceGroupId.HasValue)
            {
                var groupId = new DeviceGroupId(rollout.TargetDeviceGroupId.Value);
                targetDevices = await _deviceGroupRepository.GetDeviceIdsInGroupAsync(groupId, cancellationToken);
            }

            // 5. Get devices already assigned in previous phases
            var alreadyAssignedDeviceIds = new HashSet<DeviceId>();
            foreach (var phase in rollout.Phases)
            {
                if (phase.PhaseNumber < newPhase.PhaseNumber)
                {
                    foreach (var assignment in phase.DeviceAssignments)
                    {
                        alreadyAssignedDeviceIds.Add(assignment.DeviceId);
                    }
                }
            }

            // 6. Select devices for new phase (skip already assigned)
            var devicesForPhase = targetDevices
                .Where(d => !alreadyAssignedDeviceIds.Contains(d))
                .Take(newPhase.TargetDeviceCount)
                .ToList();

            if (devicesForPhase.Count == 0)
            {
                return Result.Failure<AdvancePhaseResponse>(
                    Error.Validation("NO_DEVICES_AVAILABLE", "No devices available for next phase."));
            }

            // 7. Assign devices to new phase
            foreach (var deviceId in devicesForPhase)
            {
                // Create device assignment
                var assignment = RolloutDeviceAssignment.Create(
                    Guid.NewGuid(),
                    rollout.Id,
                    newPhase.Id,
                    deviceId);

                newPhase.AddDeviceAssignment(assignment);
                assignment.MarkAssigned(completedAt);

                // Update device desired state
                var existingDesiredState = await _desiredStateRepository.GetByDeviceIdAsync(
                    deviceId, cancellationToken);

                if (existingDesiredState is not null)
                {
                    existingDesiredState.UpdateBundleVersion(
                        rollout.TargetVersion,
                        $"Rollout: {rollout.Name} - Phase {newPhase.PhaseNumber + 1}",
                        completedAt);
                    await _desiredStateRepository.UpdateAsync(existingDesiredState, cancellationToken);
                }
                else
                {
                    var desiredState = DeviceDesiredState.Create(
                        Guid.NewGuid(),
                        deviceId,
                        rollout.BundleId,
                        rollout.TargetVersion,
                        $"Rollout: {rollout.Name} - Phase {newPhase.PhaseNumber + 1}",
                        completedAt);
                    await _desiredStateRepository.AddAsync(desiredState, cancellationToken);
                }
            }

            // 8. Save changes
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            await _desiredStateRepository.SaveChangesAsync(cancellationToken);
            await _rolloutRepository.SaveChangesAsync(cancellationToken);

            // 9. Return response
            return Result<AdvancePhaseResponse>.Success(new AdvancePhaseResponse(
                rollout.Id,
                newPhase.PhaseNumber,
                newPhase.Name,
                devicesForPhase.Count,
                completedAt));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AdvancePhaseResponse>(
                Error.Validation("INVALID_OPERATION", ex.Message));
        }
    }
}
