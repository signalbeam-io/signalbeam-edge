using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Validator for device-specific API keys.
/// </summary>
public interface IDeviceApiKeyValidator
{
    /// <summary>
    /// Validates a device API key and returns device information if valid.
    /// </summary>
    /// <param name="apiKey">The plain text API key to validate.</param>
    /// <param name="keyPrefix">The extracted key prefix for lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with device information.</returns>
    Task<Result<DeviceApiKeyValidationResult>> ValidateAsync(
        string apiKey,
        string keyPrefix,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of device API key validation.
/// </summary>
public class DeviceApiKeyValidationResult
{
    /// <summary>
    /// The device ID that owns this API key.
    /// </summary>
    public Guid DeviceId { get; init; }

    /// <summary>
    /// The tenant ID the device belongs to.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Whether the device registration is approved.
    /// </summary>
    public bool IsApproved { get; init; }

    /// <summary>
    /// The device status.
    /// </summary>
    public string DeviceStatus { get; init; } = string.Empty;
}
