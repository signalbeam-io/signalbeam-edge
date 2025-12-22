using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;

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
        var group = app.MapGroup("/api/rollouts")
            .WithTags("Rollouts")
            .WithOpenApi();

        group.MapPost("", CreateRollout)
            .WithName("CreateRollout")
            .WithSummary("Create a new rollout")
            .WithDescription("Assigns a bundle to multiple devices or groups in one operation.");

        group.MapGet("/{rolloutId}", GetRolloutById)
            .WithName("GetRolloutById")
            .WithSummary("Get rollout by ID")
            .WithDescription("Retrieves rollout details including progress.");

        group.MapGet("/{rolloutId}/devices", GetRolloutDevices)
            .WithName("GetRolloutDevices")
            .WithSummary("Get device-level rollout status")
            .WithDescription("Retrieves status for each device in the rollout.");

        group.MapPost("/{rolloutId}/cancel", CancelRollout)
            .WithName("CancelRollout")
            .WithSummary("Cancel rollout")
            .WithDescription("Cancels all pending and in-progress devices in the rollout.");

        return app;
    }

    private static async Task<IResult> CreateRollout(
        CreateRolloutCommand command,
        CreateRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/rollouts/{result.Value!.RolloutId}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
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
