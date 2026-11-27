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
    /// Leading marker of a device API key (<c>sb_device_{prefix}_{secret}</c>).
    /// </summary>
    public const string DeviceApiKeyPrefix = "sb_device_";

    /// <summary>
    /// Whether <paramref name="apiKey"/> has the exact shape DeviceApiKeyService issues:
    /// <c>sb_device_</c>, 8 lowercase hex characters, <c>_</c>, 40 of <c>[a-z0-9]</c>.
    /// </summary>
    public static bool IsWellFormedDeviceApiKey(string apiKey)
    {
        const int prefixLength = 8;
        const int secretLength = 40;
        if (apiKey.Length != DeviceApiKeyPrefix.Length + prefixLength + 1 + secretLength
            || !apiKey.StartsWith(DeviceApiKeyPrefix, StringComparison.Ordinal)
            || apiKey[DeviceApiKeyPrefix.Length + prefixLength] != '_')
        {
            return false;
        }

        var devicePrefix = apiKey.AsSpan(DeviceApiKeyPrefix.Length, prefixLength);
        var secret = apiKey.AsSpan(DeviceApiKeyPrefix.Length + prefixLength + 1);
        foreach (var c in devicePrefix)
        {
            if (!char.IsAsciiHexDigitLower(c))
            {
                return false;
            }
        }

        foreach (var c in secret)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

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
