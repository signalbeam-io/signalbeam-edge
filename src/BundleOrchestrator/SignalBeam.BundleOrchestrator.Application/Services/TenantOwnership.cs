using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Services;

// Checks that a resource named by ID belongs to the caller's tenant (#498). Another tenant's
// resource reads as not found, with the same code as a missing one, so IDs can't be probed.
// A malformed or unknown ID passes: the handler reports it as before.
public class TenantOwnership
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IRolloutRepository _rolloutRepository;
    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public TenantOwnership(
        IBundleRepository bundleRepository,
        IRolloutRepository rolloutRepository,
        IRolloutStatusRepository rolloutStatusRepository)
    {
        _bundleRepository = bundleRepository;
        _rolloutRepository = rolloutRepository;
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Error?> CheckBundleAsync(Guid tenantId, string? bundleId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(bundleId, out var id))
        {
            return null;
        }

        var bundle = await _bundleRepository.GetByIdAsync(new BundleId(id), cancellationToken);
        return bundle is null || bundle.TenantId.Value == tenantId ? null : BundleNotFound(id);
    }

    public async Task<Error?> CheckPhasedRolloutAsync(Guid tenantId, string? rolloutId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rolloutId, out var id))
        {
            return null;
        }

        var rollout = await _rolloutRepository.GetByIdWithoutIncludesAsync(id, cancellationToken);
        return rollout is null || rollout.TenantId.Value == tenantId
            ? null
            : Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {id} not found.");
    }

    // A rollout of /api/rollouts is a set of per-device statuses for one bundle; its tenant is the bundle's.
    public async Task<Error?> CheckRolloutAsync(Guid tenantId, string? rolloutId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rolloutId, out var id))
        {
            return null;
        }

        var statuses = await _rolloutStatusRepository.GetByRolloutIdAsync(id, cancellationToken);
        return statuses.Count == 0 || await BelongsAsync(tenantId, statuses[0].BundleId, cancellationToken)
            ? null
            : Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout {id} not found.");
    }

    public async Task<Error?> CheckRolloutStatusAsync(Guid tenantId, string? rolloutStatusId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rolloutStatusId, out var id))
        {
            return null;
        }

        var status = await _rolloutStatusRepository.GetByIdAsync(id, cancellationToken);
        return status is null || await BelongsAsync(tenantId, status.BundleId, cancellationToken)
            ? null
            : Error.NotFound("ROLLOUT_NOT_FOUND", $"Rollout status {id} not found.");
    }

    private async Task<bool> BelongsAsync(Guid tenantId, BundleId bundleId, CancellationToken cancellationToken)
    {
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        return bundle is not null && bundle.TenantId.Value == tenantId;
    }

    private static Error BundleNotFound(Guid id) =>
        Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle {id} not found.");
}
