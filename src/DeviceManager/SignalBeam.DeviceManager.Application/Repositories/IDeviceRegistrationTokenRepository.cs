using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository interface for DeviceRegistrationToken aggregate.
/// </summary>
public interface IDeviceRegistrationTokenRepository
{
    /// <summary>
    /// Gets a registration token by its ID.
    /// </summary>
    Task<DeviceRegistrationToken?> GetByIdAsync(Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a registration token by its prefix (first 8 chars).
    /// </summary>
    Task<DeviceRegistrationToken?> GetByPrefixAsync(string tokenPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all valid (unused, non-expired) tokens for a tenant.
    /// </summary>
    Task<IReadOnlyList<DeviceRegistrationToken>> GetValidTokensByTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new registration token.
    /// </summary>
    Task AddAsync(DeviceRegistrationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing registration token.
    /// </summary>
    Task UpdateAsync(DeviceRegistrationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
