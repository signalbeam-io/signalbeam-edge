using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for Rollout aggregate.
/// </summary>
public interface IRolloutRepository
{
    /// <summary>
    /// Gets a rollout by ID with all phases and device assignments.
    /// </summary>
    Task<Rollout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a rollout by ID without loading related entities (phases, assignments).
    /// </summary>
    Task<Rollout?> GetByIdWithoutIncludesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rollouts for a tenant.
    /// </summary>
    Task<IReadOnlyList<Rollout>> GetAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated rollouts for a tenant with optional status filter.
    /// </summary>
    Task<(IReadOnlyList<Rollout> Rollouts, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        RolloutLifecycleStatus? status = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active rollouts (InProgress or Paused).
    /// </summary>
    Task<IReadOnlyList<Rollout>> GetActiveRolloutsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rollouts for a specific bundle.
    /// </summary>
    Task<IReadOnlyList<Rollout>> GetByBundleIdAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rollouts that are targeting a specific device (through device groups).
    /// </summary>
    Task<IReadOnlyList<Rollout>> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there's an active rollout for a bundle.
    /// </summary>
    Task<bool> HasActiveRolloutAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new rollout.
    /// </summary>
    Task AddAsync(Rollout rollout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rollout.
    /// </summary>
    Task UpdateAsync(Rollout rollout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a rollout.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
