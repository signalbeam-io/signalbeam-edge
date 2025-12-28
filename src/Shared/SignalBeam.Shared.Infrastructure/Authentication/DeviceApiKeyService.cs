using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Implementation of device API key generation and validation.
/// API Key Format: sb_device_{prefix}_{secret}
/// Example: sb_device_a1b2c3d4_9x8y7z6w5v4u3t2s1r0q9p8o7n6m5l4k3j2i1h0g
/// </summary>
public class DeviceApiKeyService : IDeviceApiKeyService
{
    private const string KeyPrefix = "sb_device_";
    private const int SecretLength = 40; // 40 characters = 240 bits of entropy
    private const int DevicePrefixLength = 8;

    private readonly ILogger<DeviceApiKeyService> _logger;

    public DeviceApiKeyService(ILogger<DeviceApiKeyService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public (string PlainTextKey, string KeyHash, string KeyPrefix) GenerateApiKey(DeviceId deviceId)
    {
        // Generate device prefix (first 8 chars of device ID)
        var devicePrefix = deviceId.Value.ToString("N")[..DevicePrefixLength];

        // Generate cryptographically secure random secret
        var secret = GenerateSecureSecret(SecretLength);

        // Construct the full API key
        var apiKey = $"{KeyPrefix}{devicePrefix}_{secret}";

        // Hash the API key using BCrypt
        var keyHash = BCrypt.Net.BCrypt.HashPassword(apiKey, workFactor: 12);

        _logger.LogInformation(
            "Generated new API key for device {DeviceId} with prefix {Prefix}",
            deviceId.Value,
            devicePrefix);

        return (apiKey, keyHash, devicePrefix);
    }

    /// <inheritdoc />
    public bool ValidateApiKey(string plainTextKey, string keyHash)
    {
        if (string.IsNullOrWhiteSpace(plainTextKey))
        {
            _logger.LogWarning("Attempted to validate empty API key");
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyHash))
        {
            _logger.LogWarning("Attempted to validate against empty key hash");
            return false;
        }

        try
        {
            // Verify the API key against the BCrypt hash
            return BCrypt.Net.BCrypt.Verify(plainTextKey, keyHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return false;
        }
    }

    /// <inheritdoc />
    public string? ExtractKeyPrefix(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        // Expected format: sb_device_{prefix}_{secret}
        if (!apiKey.StartsWith(KeyPrefix))
            return null;

        var parts = apiKey.Split('_');
        if (parts.Length != 4) // ["sb", "device", "{prefix}", "{secret}"]
            return null;

        return parts[2]; // Return the device prefix
    }

    /// <summary>
    /// Generates a cryptographically secure random string.
    /// </summary>
    private static string GenerateSecureSecret(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[length];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var result = new StringBuilder(length);
        foreach (var b in bytes)
        {
            result.Append(chars[b % chars.Length]);
        }

        return result.ToString();
    }
}
