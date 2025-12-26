using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// DTO for phase configuration.
/// </summary>
public record PhaseConfigDto(
    string Name,
    decimal TargetPercentage,
    int? MinHealthyDurationMinutes = null);

/// <summary>
/// Command to create a phased rollout with automatic progression.
/// </summary>
public record CreatePhasedRolloutCommand(
    Guid TenantId,
    string BundleId,
    string TargetVersion,
    string? PreviousVersion,
    string Name,
    string? Description,
    Guid? TargetDeviceGroupId,
    List<PhaseConfigDto> Phases,
    decimal FailureThreshold = 0.05m,
    string? CreatedBy = null);

/// <summary>
/// Response after creating a phased rollout.
/// </summary>
public record CreatePhasedRolloutResponse(
    Guid RolloutId,
    Guid BundleId,
    string TargetVersion,
    string Name,
    int PhaseCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for CreatePhasedRolloutCommand.
/// </summary>
public class CreatePhasedRolloutHandler
{
    private readonly IRolloutRepository _rolloutRepository;
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IDeviceGroupRepository _deviceGroupRepository;

    public CreatePhasedRolloutHandler(
        IRolloutRepository rolloutRepository,
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IDeviceGroupRepository deviceGroupRepository)
    {
        _rolloutRepository = rolloutRepository;
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _deviceGroupRepository = deviceGroupRepository;
    }

    public async Task<Result<CreatePhasedRolloutResponse>> Handle(
        CreatePhasedRolloutCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate tenant ID
        var tenantId = new TenantId(command.TenantId);

        // 2. Validate and parse BundleId
        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID: {command.BundleId}"));
        }

        // 3. Check bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle {command.BundleId} not found."));
        }

        // Verify tenant match
        if (bundle.TenantId != tenantId)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Forbidden("TENANT_MISMATCH", "Bundle does not belong to this tenant."));
        }

        // 4. Validate and parse target version
        if (!BundleVersion.TryParse(command.TargetVersion, out var targetVersion) || targetVersion is null)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid target version: {command.TargetVersion}"));
        }

        // 5. Check bundle version exists
        var bundleVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId, targetVersion, cancellationToken);
        if (bundleVersion is null)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {command.TargetVersion} not found for this bundle."));
        }

        // 6. Parse previous version if provided
        BundleVersion? previousVersion = null;
        if (!string.IsNullOrWhiteSpace(command.PreviousVersion))
        {
            if (!BundleVersion.TryParse(command.PreviousVersion, out previousVersion))
            {
                return Result.Failure<CreatePhasedRolloutResponse>(
                    Error.Validation("INVALID_PREVIOUS_VERSION", $"Invalid previous version: {command.PreviousVersion}"));
            }
        }

        // 7. Validate device group if specified
        if (command.TargetDeviceGroupId.HasValue)
        {
            var groupId = new DeviceGroupId(command.TargetDeviceGroupId.Value);
            var group = await _deviceGroupRepository.GetByIdAsync(groupId, cancellationToken);
            if (group is null)
            {
                return Result.Failure<CreatePhasedRolloutResponse>(
                    Error.NotFound("GROUP_NOT_FOUND", $"Device group {command.TargetDeviceGroupId} not found."));
            }
        }

        // 8. Validate phases
        if (command.Phases == null || command.Phases.Count == 0)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Validation("NO_PHASES", "At least one phase is required."));
        }

        // Validate percentage totals to 100%
        var totalPercentage = command.Phases.Sum(p => p.TargetPercentage);
        if (totalPercentage != 100m)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Validation("INVALID_PHASE_PERCENTAGES",
                    $"Phase percentages must total 100%, got {totalPercentage}%."));
        }

        // 9. Check for active rollouts on this bundle
        var hasActiveRollout = await _rolloutRepository.HasActiveRolloutAsync(bundleId, cancellationToken);
        if (hasActiveRollout)
        {
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Conflict("ACTIVE_ROLLOUT_EXISTS",
                    "An active rollout already exists for this bundle. Pause or complete it before creating a new one."));
        }

        // 10. Get total device count for the target group to calculate phase sizes
        int totalDeviceCount = 0;
        if (command.TargetDeviceGroupId.HasValue)
        {
            var groupId = new DeviceGroupId(command.TargetDeviceGroupId.Value);
            var deviceIds = await _deviceGroupRepository.GetDeviceIdsInGroupAsync(groupId, cancellationToken);
            totalDeviceCount = deviceIds.Count;

            if (totalDeviceCount == 0)
            {
                return Result.Failure<CreatePhasedRolloutResponse>(
                    Error.Validation("NO_DEVICES_IN_GROUP", "Target device group has no devices."));
            }
        }
        else
        {
            // If no group specified, we'll need to get all devices for the tenant
            // For now, require a target group
            return Result.Failure<CreatePhasedRolloutResponse>(
                Error.Validation("TARGET_GROUP_REQUIRED", "Target device group is required for phased rollouts."));
        }

        // 11. Create rollout aggregate
        var rolloutId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var rollout = Rollout.Create(
            rolloutId,
            tenantId,
            bundleId,
            targetVersion,
            previousVersion,
            command.Name,
            command.Description,
            command.TargetDeviceGroupId,
            command.CreatedBy,
            command.FailureThreshold,
            createdAt);

        // 12. Create phases
        int phaseNumber = 0;
        foreach (var phaseConfig in command.Phases)
        {
            var targetDeviceCount = (int)Math.Ceiling(totalDeviceCount * (phaseConfig.TargetPercentage / 100m));

            var minHealthyDuration = phaseConfig.MinHealthyDurationMinutes.HasValue
                ? TimeSpan.FromMinutes(phaseConfig.MinHealthyDurationMinutes.Value)
                : (TimeSpan?)null;

            var phase = RolloutPhase.Create(
                Guid.NewGuid(),
                rolloutId,
                phaseNumber,
                phaseConfig.Name,
                targetDeviceCount,
                phaseConfig.TargetPercentage,
                minHealthyDuration);

            rollout.AddPhase(phase);
            phaseNumber++;
        }

        // 13. Save rollout
        await _rolloutRepository.AddAsync(rollout, cancellationToken);
        await _rolloutRepository.SaveChangesAsync(cancellationToken);

        // 14. Return response
        return Result<CreatePhasedRolloutResponse>.Success(new CreatePhasedRolloutResponse(
            rolloutId,
            bundleId.Value,
            targetVersion.ToString(),
            command.Name,
            command.Phases.Count,
            createdAt));
    }
}
