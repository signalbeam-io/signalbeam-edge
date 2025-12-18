using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to assign a bundle version to a single device.
/// </summary>
public record AssignBundleToDeviceCommand(
    string DeviceId,
    string BundleId,
    string Version,
    string? AssignedBy = null);

/// <summary>
/// Response after assigning a bundle to a device.
/// </summary>
public record AssignBundleToDeviceResponse(
    Guid DeviceId,
    Guid BundleId,
    string Version,
    DateTimeOffset AssignedAt);

/// <summary>
/// Handler for AssignBundleToDeviceCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class AssignBundleToDeviceHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public AssignBundleToDeviceHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IDeviceDesiredStateRepository desiredStateRepository,
        IRolloutStatusRepository rolloutStatusRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _desiredStateRepository = desiredStateRepository;
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<AssignBundleToDeviceResponse>> Handle(
        AssignBundleToDeviceCommand command,
        CancellationToken cancellationToken)
    {
        // Parse and validate device ID
        if (!DeviceId.TryParse(command.DeviceId, out var deviceId))
        {
            return Result.Failure<AssignBundleToDeviceResponse>(
                Error.Validation("INVALID_DEVICE_ID", $"Invalid device ID format: {command.DeviceId}"));
        }

        // Parse and validate bundle ID
        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<AssignBundleToDeviceResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {command.BundleId}"));
        }

        // Check if bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<AssignBundleToDeviceResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {command.BundleId} not found."));
        }

        // Parse and validate version
        if (!BundleVersion.TryParse(command.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<AssignBundleToDeviceResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Version}"));
        }

        // Check if bundle version exists
        var appBundleVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (appBundleVersion is null)
        {
            return Result.Failure<AssignBundleToDeviceResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {command.Version} not found for bundle {bundle.Name}."));
        }

        var assignedAt = DateTimeOffset.UtcNow;

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

        // Create rollout status entry
        var rolloutStatus = RolloutStatus.Create(
            Guid.NewGuid(),
            bundleId,
            bundleVersion,
            deviceId,
            assignedAt);

        await _rolloutStatusRepository.AddAsync(rolloutStatus, cancellationToken);

        // Save all changes
        await _desiredStateRepository.SaveChangesAsync(cancellationToken);
        await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<AssignBundleToDeviceResponse>.Success(new AssignBundleToDeviceResponse(
            deviceId.Value,
            bundleId.Value,
            bundleVersion.ToString(),
            assignedAt));
    }
}
