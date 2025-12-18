using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for AppBundle aggregate.
/// </summary>
public interface IBundleRepository
{
    /// <summary>
    /// Gets a bundle by its ID.
    /// </summary>
    Task<AppBundle?> GetByIdAsync(BundleId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a bundle by name and tenant.
    /// </summary>
    Task<AppBundle?> GetByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all bundles for a tenant.
    /// </summary>
    Task<IReadOnlyList<AppBundle>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new bundle.
    /// </summary>
    Task AddAsync(AppBundle bundle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing bundle.
    /// </summary>
    Task UpdateAsync(AppBundle bundle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bundle.
    /// </summary>
    Task DeleteAsync(BundleId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
