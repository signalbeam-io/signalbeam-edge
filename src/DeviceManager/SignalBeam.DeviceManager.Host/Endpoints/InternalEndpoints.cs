using Microsoft.AspNetCore.Mvc;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.DeviceManager.Host.Endpoints;

// Service-to-service endpoints. Not routed by the gateway; callers authenticate with the
// internal service key instead of user or device credentials.
public static class InternalEndpoints
{
    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(InternalServiceAuthentication.PathPrefix)
            .WithTags("Internal")
            .AllowAnonymous() // Authenticated by RequireInternalServiceKey, not user/device auth.
            .RequireInternalServiceKey()
            // Callers carry no tenant claim, so the global limiter would put them in the shared
            // "anonymous" bucket with public registration traffic and starve alert tenant lookups.
            // These endpoints are only reachable inside the environment and reject bad keys cheaply.
            .DisableRateLimiting()
            .ExcludeFromDescription();

        group.MapGet("/devices/{deviceId:guid}/tenant", GetDeviceTenant)
            .WithName("GetDeviceTenant");

        return app;
    }

    private static async Task<IResult> GetDeviceTenant(
        Guid deviceId,
        [FromServices] GetDeviceTenantHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetDeviceTenantQuery(deviceId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error!.Code, message = result.Error.Message, type = result.Error.Type.ToString() });
    }
}
