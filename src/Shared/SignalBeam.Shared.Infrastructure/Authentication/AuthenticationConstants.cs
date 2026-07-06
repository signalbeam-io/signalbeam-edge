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

    /// <summary>
    /// Device API key authentication scheme name.
    /// </summary>
    public const string DeviceApiKeyScheme = "DeviceApiKey";

    /// <summary>
    /// Certificate (mTLS) authentication scheme name.
    /// </summary>
    public const string CertificateScheme = "Certificate";

    /// <summary>
    /// Claim type identifying how the caller authenticated. Set by the auth middleware to an
    /// unambiguous value (see the <c>AuthMethod*</c> constants) so authorization policies can
    /// distinguish an operator JWT from a device or tenant API key regardless of the ClaimsIdentity
    /// scheme name (which overloads "ApiKey" between device and tenant keys).
    /// </summary>
    public const string AuthMethodClaimType = "auth_method";

    /// <summary>Operator authenticated via a Zitadel/OIDC JWT.</summary>
    public const string AuthMethodJwt = "Jwt";

    /// <summary>Caller authenticated via a config-backed plaintext tenant API key (dev-only).</summary>
    public const string AuthMethodTenantApiKey = "TenantApiKey";

    /// <summary>Device authenticated via a hashed device API key (<c>sb_device_*</c>).</summary>
    public const string AuthMethodDeviceApiKey = "DeviceApiKey";

    /// <summary>Device authenticated via a client certificate (mTLS).</summary>
    public const string AuthMethodCertificate = "Certificate";
}
