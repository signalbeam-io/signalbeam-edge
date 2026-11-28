using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Unified Rollout API endpoints.
/// </summary>
public static class RolloutEndpoints
{
    /// <summary>
    /// Maps all rollout-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapRolloutEndpoints(this IEndpointRouteBuilder app)
    {
        // Rollout management is operator-only — the agent reports reconciliation status but never
        // drives rollouts.
        var group = app.MapGroup("/api/rollouts")
            .WithTags("Rollouts")
            .RequireAuthorization(AuthorizationPolicies.OperatorAccess)
            .RequireCallerTenant();

        group.MapGet("", GetRollouts)
            .WithName("GetRollouts")
            .WithSummary("Get paginated list of rollouts")
            .WithDescription("Retrieves the caller's tenant's rollouts, optionally filtered by bundle ID.");

        group.MapPost("", CreateRollout)
            .WithName("CreateRollout")
            .WithSummary("Create a new rollout")
            .WithDescription("Assigns a bundle to multiple devices or groups in one operation.");

        group.MapGet("/{rolloutId}", GetRolloutById)
            .WithName("GetRolloutById")
            .RequireOwnRollout()
            .WithSummary("Get rollout by ID")
            .WithDescription("Retrieves rollout details including progress.");

        group.MapGet("/{rolloutId}/devices", GetRolloutDevices)
            .WithName("GetRolloutDevices")
            .RequireOwnRollout()
            .WithSummary("Get device-level rollout status")
            .WithDescription("Retrieves status for each device in the rollout.");

        group.MapPost("/{rolloutId}/cancel", CancelRollout)
            .WithName("CancelRollout")
            .RequireOwnRollout()
            .WithSummary("Cancel rollout")
            .WithDescription("Cancels all pending and in-progress devices in the rollout.");

        return app;
    }

    private static async Task<IResult> GetRollouts(
        string? bundleId,
        HttpContext context,
        GetRolloutsHandler handler,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 10)
    {
        var query = new GetRolloutsQuery(TenantScoping.CallerTenant(context)!.Value, bundleId, page, pageSize);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> CreateRollout(
        CreateRolloutCommand command,
        HttpContext context,
        CreateRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, TenantScoping.CallerTenant(context)!.Value, cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Created($"/api/rollouts/{result.Value!.RolloutId}", result.Value);
        }

        // Another tenant's bundle or device is 404; an unreachable DeviceManager 503; the rest 400.
        var error = result.Error!;
        if (DependencyErrors.IsUnavailable(error))
        {
            return DependencyErrors.ServiceUnavailable(error);
        }

        return error.Type == ErrorType.NotFound
            ? TenantScoping.NotFound(error)
            : Results.BadRequest(new { error = error.Code, message = error.Message, type = error.Type.ToString() });
    }

    private static async Task<IResult> GetRolloutById(
        string rolloutId,
        GetRolloutByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRolloutByIdQuery(rolloutId);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new
            {
                error = result.Error!.Code,
                message = result.Error.Message
            });
    }

    private static async Task<IResult> GetRolloutDevices(
        string rolloutId,
        GetRolloutDevicesHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRolloutDevicesQuery(rolloutId);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new
            {
                error = result.Error!.Code,
                message = result.Error.Message
            });
    }

    private static async Task<IResult> CancelRollout(
        string rolloutId,
        CancelRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CancelRolloutCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message
            });
    }
}
