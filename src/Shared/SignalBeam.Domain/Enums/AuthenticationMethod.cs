namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the authentication method used by a device.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// Device authenticated via API key in X-API-Key header.
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// Device authenticated via mTLS client certificate.
    /// </summary>
    Certificate = 2,

    /// <summary>
    /// Device authenticated via both API key and certificate (dual authentication).
    /// Not currently used, reserved for future enhancement.
    /// </summary>
    ApiKeyAndCertificate = 3
}
