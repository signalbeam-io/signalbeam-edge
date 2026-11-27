using Microsoft.AspNetCore.Authorization;

namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// Requires a device credential (device API key or certificate) whose device ID equals the
/// <c>{deviceId}</c> route value, so one device can never read or report for another.
/// <see cref="AllowOperatorJwt"/> also admits an operator JWT, and <see cref="AllowTenantApiKeyFallback"/>
/// the plaintext tenant key (non-Production only, as for <see cref="OperatorAccessRequirement"/>).
/// Tenant ownership of the device is still checked by the endpoint for callers that aren't the device.
/// </summary>
public sealed class DeviceAccessRequirement : IAuthorizationRequirement
{
    public const string DeviceIdRouteValue = "deviceId";

    public DeviceAccessRequirement(bool allowOperatorJwt, bool allowTenantApiKeyFallback)
    {
        AllowOperatorJwt = allowOperatorJwt;
        AllowTenantApiKeyFallback = allowTenantApiKeyFallback;
    }

    public bool AllowOperatorJwt { get; }

    public bool AllowTenantApiKeyFallback { get; }
}
