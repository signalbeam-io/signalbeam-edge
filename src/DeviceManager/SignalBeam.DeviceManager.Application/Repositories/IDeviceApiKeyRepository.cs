using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository for DeviceApiKey entity.
/// </summary>
public interface IDeviceApiKeyRepository
{
    /// <summary>
    /// Gets an API key by its ID.
    /// </summary>
    Task<DeviceApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an active (non-revoked, non-expired) API key by its prefix.
    /// </summary>
    Task<DeviceApiKey?> GetActiveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active API keys for a device.
    /// </summary>
    Task<IReadOnlyList<DeviceApiKey>> GetActiveByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new API key.
    /// </summary>
    Task AddAsync(DeviceApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an API key.
    /// </summary>
    void Update(DeviceApiKey apiKey);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
