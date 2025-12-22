using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for RolloutStatus aggregate.
/// </summary>
public interface IRolloutStatusRepository
{
    /// <summary>
    /// Gets rollout status by ID.
    /// </summary>
    Task<RolloutStatus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rollout status for a specific device and bundle.
    /// </summary>
    Task<RolloutStatus?> GetByDeviceAndBundleAsync(
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rollout statuses for a bundle.
    /// </summary>
    Task<IReadOnlyList<RolloutStatus>> GetByBundleAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rollout statuses for a device.
    /// </summary>
    Task<IReadOnlyList<RolloutStatus>> GetByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rollout statuses for a specific rollout ID.
    /// </summary>
    Task<IReadOnlyList<RolloutStatus>> GetByRolloutIdAsync(
        Guid rolloutId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new rollout status.
    /// </summary>
    Task AddAsync(RolloutStatus rolloutStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rollout status.
    /// </summary>
    Task UpdateAsync(RolloutStatus rolloutStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
