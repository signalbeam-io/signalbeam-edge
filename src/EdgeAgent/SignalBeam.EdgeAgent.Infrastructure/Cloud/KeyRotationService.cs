using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Cloud;

/// <summary>
/// Rotates the device API key before it expires. The agent must rotate while its current key
/// is still valid (rotation is authenticated by that key); once expired, rotation is no longer
/// possible and the device must be re-provisioned.
/// </summary>
public sealed class KeyRotationService : IKeyRotationService
{
    private readonly ICloudClient _cloudClient;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<KeyRotationService> _logger;
    private readonly int _rotationThresholdDays;

    public KeyRotationService(
        ICloudClient cloudClient,
        IDeviceCredentialsStore credentialsStore,
        ILogger<KeyRotationService> logger,
        int rotationThresholdDays)
    {
        _cloudClient = cloudClient;
        _credentialsStore = credentialsStore;
        _logger = logger;
        _rotationThresholdDays = rotationThresholdDays;
    }

    public async Task<bool> CheckAndRotateAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await _credentialsStore.LoadCredentialsAsync(cancellationToken);
        if (credentials is null || string.IsNullOrEmpty(credentials.ApiKey))
        {
            return false;
        }

        // A key with no expiry never needs rotation.
        if (!credentials.ApiKeyExpiresAt.HasValue)
        {
            return false;
        }

        var daysUntilExpiry = (credentials.ApiKeyExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;
        if (daysUntilExpiry > _rotationThresholdDays)
        {
            return false;
        }

        if (daysUntilExpiry <= 0)
        {
            // Already expired — rotation can't authenticate; surface for operator intervention.
            _logger.LogError(
                "Device {DeviceId} API key has expired; automatic rotation is no longer possible.",
                credentials.DeviceId);
            return false;
        }

        _logger.LogInformation(
            "Device {DeviceId} API key expires in {Days:F1} days; rotating.",
            credentials.DeviceId, daysUntilExpiry);

        var rotated = await _cloudClient.RotateApiKeyAsync(credentials.DeviceId, cancellationToken);

        credentials.ApiKey = rotated.ApiKey;
        credentials.ApiKeyExpiresAt = rotated.ExpiresAt;
        await _credentialsStore.SaveCredentialsAsync(credentials, cancellationToken);

        _logger.LogInformation(
            "Device {DeviceId} API key rotated; new expiry {Expiry:O}.",
            credentials.DeviceId, rotated.ExpiresAt);

        return true;
    }
}
