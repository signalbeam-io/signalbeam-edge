namespace SignalBeam.DeviceManager.Host;

/// <summary>
/// Named rate-limiter policy keys applied to specific endpoints (on top of the global limiter).
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Per-client-IP limit for the anonymous device-registration handshake (POST /api/devices),
    /// so a single source cannot flood a tenant with Pending device records.
    /// </summary>
    public const string DeviceRegistration = "device-registration";
}

/// <summary>
/// Options for the per-client-IP registration rate limit. Bound from the
/// <c>RateLimiting:Registration</c> configuration section and resolved per request so tests can
/// override it via DI.
/// </summary>
public sealed class RegistrationRateLimitOptions
{
    public const string SectionName = "RateLimiting:Registration";

    /// <summary>Max registration requests per window per client IP.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Fixed window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
