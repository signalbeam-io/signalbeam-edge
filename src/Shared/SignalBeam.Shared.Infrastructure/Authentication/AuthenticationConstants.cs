namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Constants for authentication.
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// API key header name.
    /// </summary>
    public const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>
    /// Tenant ID claim type.
    /// </summary>
    public const string TenantIdClaimType = "tenant_id";

    /// <summary>
    /// Device ID claim type (for device authentication).
    /// </summary>
    public const string DeviceIdClaimType = "device_id";

    /// <summary>
    /// Scope claim type.
    /// </summary>
    public const string ScopeClaimType = "scope";

    /// <summary>
    /// API key authentication scheme name.
    /// </summary>
    public const string ApiKeyScheme = "ApiKey";

    /// <summary>
    /// JWT Bearer authentication scheme name.
    /// </summary>
    public const string JwtBearerScheme = "Bearer";

    /// <summary>
    /// Device authentication scheme name (for edge devices).
    /// </summary>
    public const string DeviceScheme = "Device";
}
