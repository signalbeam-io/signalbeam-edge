using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to assign a bundle version to all devices in a group.
/// </summary>
public record AssignBundleToGroupCommand(
    string DeviceGroupId,
    string BundleId,
    string Version,
    string? AssignedBy = null);

/// <summary>
/// Response after assigning a bundle to a group.
/// </summary>
public record AssignBundleToGroupResponse(
    Guid DeviceGroupId,
    Guid BundleId,
    string Version,
    int DeviceCount,
    DateTimeOffset AssignedAt);

/// <summary>
/// Handler for AssignBundleToGroupCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class AssignBundleToGroupHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IDeviceGroupRepository _deviceGroupRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public AssignBundleToGroupHandler(
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

    public async Task<Result<AssignBundleToGroupResponse>> Handle(
        AssignBundleToGroupCommand command,
        CancellationToken cancellationToken)
    {
        // Parse and validate device group ID
        if (!DeviceGroupId.TryParse(command.DeviceGroupId, out var deviceGroupId))
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.Validation("INVALID_GROUP_ID", $"Invalid device group ID format: {command.DeviceGroupId}"));
        }

        // Check if device group exists
        var deviceGroup = await _deviceGroupRepository.GetByIdAsync(deviceGroupId, cancellationToken);
        if (deviceGroup is null)
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.NotFound("GROUP_NOT_FOUND", $"Device group with ID {command.DeviceGroupId} not found."));
        }

        // Parse and validate bundle ID
        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {command.BundleId}"));
        }

        // Check if bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {command.BundleId} not found."));
        }

        // Parse and validate version
        if (!BundleVersion.TryParse(command.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Version}"));
        }

        // Check if bundle version exists
        var appBundleVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (appBundleVersion is null)
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {command.Version} not found for bundle {bundle.Name}."));
        }

        // Get all devices in the group
        var deviceIds = await _deviceGroupRepository.GetDeviceIdsInGroupAsync(deviceGroupId, cancellationToken);

        if (deviceIds.Count == 0)
        {
            return Result.Failure<AssignBundleToGroupResponse>(
                Error.Validation("EMPTY_GROUP", $"Device group {deviceGroup.Name} has no devices."));
        }

        var assignedAt = DateTimeOffset.UtcNow;

        // Generate a single rollout ID for all devices in the group
        var rolloutId = Guid.NewGuid();

        // Assign bundle to each device in the group
        foreach (var deviceId in deviceIds)
        {
            // Get or create desired state
            var existingDesiredState = await _desiredStateRepository.GetByDeviceIdAsync(deviceId, cancellationToken);

            if (existingDesiredState is not null)
            {
                // Update existing desired state
                existingDesiredState.UpdateBundleVersion(bundleVersion, command.AssignedBy, assignedAt);
                await _desiredStateRepository.UpdateAsync(existingDesiredState, cancellationToken);
            }
            else
            {
                // Create new desired state
                var desiredState = DeviceDesiredState.Create(
                    Guid.NewGuid(),
                    deviceId,
                    bundleId,
                    bundleVersion,
                    command.AssignedBy,
                    assignedAt);

                await _desiredStateRepository.AddAsync(desiredState, cancellationToken);
            }

            // Create rollout status entry for each device (all share the same rolloutId)
            var rolloutStatus = RolloutStatus.Create(
                Guid.NewGuid(),
                rolloutId,
                bundleId,
                bundleVersion,
                deviceId,
                assignedAt);

            await _rolloutStatusRepository.AddAsync(rolloutStatus, cancellationToken);
        }

        // Save all changes
        await _desiredStateRepository.SaveChangesAsync(cancellationToken);
        await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<AssignBundleToGroupResponse>.Success(new AssignBundleToGroupResponse(
            deviceGroupId.Value,
            bundleId.Value,
            bundleVersion.ToString(),
            deviceIds.Count,
            assignedAt));
    }
}
