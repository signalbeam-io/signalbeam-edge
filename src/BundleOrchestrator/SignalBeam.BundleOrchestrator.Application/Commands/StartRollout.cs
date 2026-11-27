using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to start a pending rollout.
/// </summary>
public record StartRolloutCommand(
    Guid RolloutId);

/// <summary>
/// Response after starting a rollout.
/// </summary>
public record StartRolloutResponse(
    Guid RolloutId,
    DateTimeOffset StartedAt,
    int CurrentPhaseNumber,
    string CurrentPhaseName);

/// <summary>
/// Handler for StartRolloutCommand.
/// </summary>
public class StartRolloutHandler
{
    private readonly IRolloutRepository _rolloutRepository;
    private readonly IDeviceGroupClient _deviceGroupClient;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;

    public StartRolloutHandler(
        IRolloutRepository rolloutRepository,
        IDeviceGroupClient deviceGroupClient,
        IDeviceDesiredStateRepository desiredStateRepository)
    {
        _rolloutRepository = rolloutRepository;
        _deviceGroupClient = deviceGroupClient;
        _desiredStateRepository = desiredStateRepository;
    }

    public async Task<Result<StartRolloutResponse>> Handle(
        StartRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load rollout with phases
        var rollout = await _rolloutRepository.GetByIdAsync(command.RolloutId, cancellationToken);
        if (rollout is null)
        {
            return Result.Failure<StartRolloutResponse>(
                Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {command.RolloutId} not found."));
        }

        // 2. Start the rollout (domain logic validates status)
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            rollout.Start(startedAt);

            // 3. Start the first phase
            rollout.StartCurrentPhase(startedAt);

            // 4. Get devices for the target group
            IReadOnlyList<DeviceId> targetDevices = new List<DeviceId>();
            if (rollout.TargetDeviceGroupId.HasValue)
            {
                var lookup = await _deviceGroupClient.GetAsync(
                    rollout.TenantId.Value, rollout.TargetDeviceGroupId.Value, cancellationToken);
                if (lookup.Group is null)
                {
                    return Result.Failure<StartRolloutResponse>(DeviceGroupErrors.From(lookup, rollout.TargetDeviceGroupId.Value));
                }

                targetDevices = lookup.Group.DeviceIds.Select(id => new DeviceId(id)).ToList();

                if (targetDevices.Count == 0)
                {
                    return Result.Failure<StartRolloutResponse>(
                        Error.Validation("NO_DEVICES", "Target device group has no devices."));
                }
            }

            // 5. Assign devices to first phase
            var currentPhase = rollout.GetCurrentPhase();
            if (currentPhase is null)
            {
                return Result.Failure<StartRolloutResponse>(
                    Error.Validation("NO_PHASES", "Rollout has no phases."));
            }

            // Take the first N devices for this phase
            var devicesForPhase = targetDevices.Take(currentPhase.TargetDeviceCount).ToList();

            foreach (var deviceId in devicesForPhase)
            {
                // Create device assignment
                var assignment = RolloutDeviceAssignment.Create(
                    Guid.NewGuid(),
                    rollout.Id,
                    currentPhase.Id,
                    deviceId);

                currentPhase.AddDeviceAssignment(assignment);

                // Mark as assigned and update desired state
                assignment.MarkAssigned(startedAt);

                // Update device desired state
                var existingDesiredState = await _desiredStateRepository.GetByDeviceIdAsync(
                    deviceId, cancellationToken);

                if (existingDesiredState is not null)
                {
                    existingDesiredState.UpdateBundleVersion(
                        rollout.TargetVersion,
                        $"Rollout: {rollout.Name}",
                        startedAt);
                    await _desiredStateRepository.UpdateAsync(existingDesiredState, cancellationToken);
                }
                else
                {
                    var desiredState = DeviceDesiredState.Create(
                        Guid.NewGuid(),
                        deviceId,
                        rollout.BundleId,
                        rollout.TargetVersion,
                        $"Rollout: {rollout.Name}",
                        startedAt);
                    await _desiredStateRepository.AddAsync(desiredState, cancellationToken);
                }
            }

            // 6. Save changes
            await _rolloutRepository.UpdateAsync(rollout, cancellationToken);
            await _desiredStateRepository.SaveChangesAsync(cancellationToken);
            await _rolloutRepository.SaveChangesAsync(cancellationToken);

            // 7. Return response
            return Result<StartRolloutResponse>.Success(new StartRolloutResponse(
                rollout.Id,
                startedAt,
                rollout.CurrentPhaseNumber,
                currentPhase.Name));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StartRolloutResponse>(
                Error.Validation("INVALID_OPERATION", ex.Message));
        }
    }
}
