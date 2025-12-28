using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating device API keys.
/// </summary>
public interface IDeviceApiKeyService
{
    /// <summary>
    /// Generates a new device API key.
    /// </summary>
    /// <param name="deviceId">The device ID to generate the key for.</param>
    /// <returns>A tuple containing the plain text API key and its BCrypt hash.</returns>
    (string PlainTextKey, string KeyHash, string KeyPrefix) GenerateApiKey(DeviceId deviceId);

    /// <summary>
    /// Validates a plain text API key against a stored hash.
    /// </summary>
    /// <param name="plainTextKey">The plain text API key to validate.</param>
    /// <param name="keyHash">The BCrypt hash to validate against.</param>
    /// <returns>True if the key is valid, false otherwise.</returns>
    bool ValidateApiKey(string plainTextKey, string keyHash);

    /// <summary>
    /// Extracts the device ID prefix from an API key.
    /// </summary>
    /// <param name="apiKey">The API key to extract from.</param>
    /// <returns>The device ID prefix, or null if the format is invalid.</returns>
    string? ExtractKeyPrefix(string apiKey);
}
