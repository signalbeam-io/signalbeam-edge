using SignalBeam.EdgeAgent.Application.Models;

namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Service for storing and retrieving device credentials locally.
/// </summary>
public interface IDeviceCredentialsStore
{
    /// <summary>
    /// Saves device credentials to local storage.
    /// </summary>
    Task SaveCredentialsAsync(DeviceCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads device credentials from local storage.
    /// </summary>
    Task<DeviceCredentials?> LoadCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if device credentials exist.
    /// </summary>
    Task<bool> CredentialsExistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes device credentials from local storage.
    /// </summary>
    Task DeleteCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the API key in the stored credentials.
    /// </summary>
    Task UpdateApiKeyAsync(string apiKey, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default);
}
