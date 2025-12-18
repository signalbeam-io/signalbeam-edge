using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for AppBundleVersion aggregate.
/// </summary>
public interface IBundleVersionRepository
{
    /// <summary>
    /// Gets a bundle version by ID.
    /// </summary>
    Task<AppBundleVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version of a bundle.
    /// </summary>
    Task<AppBundleVersion?> GetByBundleAndVersionAsync(
        BundleId bundleId,
        BundleVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all versions of a bundle.
    /// </summary>
    Task<IReadOnlyList<AppBundleVersion>> GetAllVersionsAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new bundle version.
    /// </summary>
    Task AddAsync(AppBundleVersion bundleVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing bundle version.
    /// </summary>
    Task UpdateAsync(AppBundleVersion bundleVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
