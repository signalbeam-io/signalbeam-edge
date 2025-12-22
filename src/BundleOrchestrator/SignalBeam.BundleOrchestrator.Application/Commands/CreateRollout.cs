using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to create a rollout - assigns bundle to multiple devices/groups in one operation.
/// </summary>
public record CreateRolloutCommand(
    string BundleId,
    string Version,
    string TargetType,
    List<string> TargetIds,
    string? AssignedBy = null);

/// <summary>
/// Response after creating a rollout.
/// </summary>
public record CreateRolloutResponse(
    Guid RolloutId,
    Guid BundleId,
    string Version,
    string TargetType,
    int TotalDevices,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for CreateRolloutCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class CreateRolloutHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IDeviceGroupRepository _deviceGroupRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public CreateRolloutHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IDeviceGroupRepository deviceGroupRepository,
        IDeviceDesiredStateRepository desiredStateRepository,
        IRolloutStatusRepository rolloutStatusRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _deviceGroupRepository = deviceGroupRepository;
        _desiredStateRepository = desiredStateRepository;
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<CreateRolloutResponse>> Handle(
        CreateRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate and parse BundleId
        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID: {command.BundleId}"));
        }

        // 2. Check bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle {command.BundleId} not found."));
        }

        // 3. Validate and parse Version
        if (!BundleVersion.TryParse(command.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid version: {command.Version}"));
        }

        // 4. Check bundle version exists
        var appBundleVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId, bundleVersion, cancellationToken);
        if (appBundleVersion is null)
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {command.Version} not found."));
        }

        // 5. Resolve target devices
        var deviceIds = new List<DeviceId>();

        if (command.TargetType.Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            // Expand groups to device IDs
            foreach (var targetId in command.TargetIds)
            {
                if (!DeviceGroupId.TryParse(targetId, out var groupId))
                {
                    return Result.Failure<CreateRolloutResponse>(
                        Error.Validation("INVALID_GROUP_ID", $"Invalid group ID: {targetId}"));
                }

                var group = await _deviceGroupRepository.GetByIdAsync(groupId, cancellationToken);
                if (group is null)
                {
                    return Result.Failure<CreateRolloutResponse>(
                        Error.NotFound("GROUP_NOT_FOUND", $"Group {targetId} not found."));
                }

                var groupDeviceIds = await _deviceGroupRepository.GetDeviceIdsInGroupAsync(
                    groupId, cancellationToken);
                deviceIds.AddRange(groupDeviceIds);
            }
        }
        else if (command.TargetType.Equals("device", StringComparison.OrdinalIgnoreCase))
        {
            // Parse device IDs directly
            foreach (var targetId in command.TargetIds)
            {
                if (!DeviceId.TryParse(targetId, out var deviceId))
                {
                    return Result.Failure<CreateRolloutResponse>(
                        Error.Validation("INVALID_DEVICE_ID", $"Invalid device ID: {targetId}"));
                }

                deviceIds.Add(deviceId);
            }
        }
        else
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.Validation("INVALID_TARGET_TYPE", "TargetType must be 'device' or 'group'."));
        }

        // Remove duplicates
        deviceIds = deviceIds.Distinct().ToList();

        if (deviceIds.Count == 0)
        {
            return Result.Failure<CreateRolloutResponse>(
                Error.Validation("NO_DEVICES", "No devices to assign bundle to."));
        }

        // 6. Generate rollout ID (groups all RolloutStatus entries)
        var rolloutId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        // 7. Create desired state and rollout status for each device
        foreach (var deviceId in deviceIds)
        {
            // Update or create desired state
            var existingDesiredState = await _desiredStateRepository.GetByDeviceIdAsync(
                deviceId, cancellationToken);

            if (existingDesiredState is not null)
            {
                existingDesiredState.UpdateBundleVersion(bundleVersion, command.AssignedBy, createdAt);
                await _desiredStateRepository.UpdateAsync(existingDesiredState, cancellationToken);
            }
            else
            {
                var desiredState = DeviceDesiredState.Create(
                    Guid.NewGuid(), deviceId, bundleId, bundleVersion,
                    command.AssignedBy, createdAt);
                await _desiredStateRepository.AddAsync(desiredState, cancellationToken);
            }

            // Create rollout status entry (with shared rolloutId)
            var rolloutStatus = RolloutStatus.Create(
                Guid.NewGuid(),
                rolloutId,
                bundleId,
                bundleVersion,
                deviceId,
                createdAt);

            await _rolloutStatusRepository.AddAsync(rolloutStatus, cancellationToken);
        }

        // 8. Save all changes
        await _desiredStateRepository.SaveChangesAsync(cancellationToken);
        await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

        // 9. Return response
        return Result<CreateRolloutResponse>.Success(new CreateRolloutResponse(
            rolloutId,
            bundleId.Value,
            bundleVersion.ToString(),
            command.TargetType,
            deviceIds.Count,
            createdAt));
    }
}
