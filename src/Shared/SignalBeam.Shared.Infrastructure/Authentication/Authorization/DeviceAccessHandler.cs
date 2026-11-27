using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SignalBeam.Shared.Infrastructure.Authentication.Authorization;

/// <summary>
/// Evaluates <see cref="DeviceAccessRequirement"/> from the middleware-stamped
/// <see cref="AuthenticationConstants.AuthMethodClaimType"/> claim and the route's device ID.
/// </summary>
public sealed class DeviceAccessHandler : AuthorizationHandler<DeviceAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DeviceAccessRequirement requirement)
    {
        var authMethod = context.User.FindFirst(AuthenticationConstants.AuthMethodClaimType)?.Value;

        var authorized = authMethod switch
        {
            AuthenticationConstants.AuthMethodDeviceApiKey or AuthenticationConstants.AuthMethodCertificate
                => IsOwnDevice(context),
            AuthenticationConstants.AuthMethodJwt => requirement.AllowOperatorJwt,
            AuthenticationConstants.AuthMethodTenantApiKey => requirement.AllowTenantApiKeyFallback,
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsOwnDevice(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext
            || !Guid.TryParse(httpContext.GetRouteValue(DeviceAccessRequirement.DeviceIdRouteValue)?.ToString(), out var routeDeviceId)
            || !Guid.TryParse(context.User.FindFirst(AuthenticationConstants.DeviceIdClaimType)?.Value, out var callerDeviceId))
        {
            return false;
        }

        return routeDeviceId != Guid.Empty && routeDeviceId == callerDeviceId;
    }
}
