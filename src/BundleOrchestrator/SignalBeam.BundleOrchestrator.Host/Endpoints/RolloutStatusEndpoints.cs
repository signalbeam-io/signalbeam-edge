using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Rollout Status API endpoints.
/// </summary>
public static class RolloutStatusEndpoints
{
    /// <summary>
    /// Maps all rollout status-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapRolloutStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rollouts")
            .WithTags("Rollout Status")
            .WithOpenApi();

        group.MapGet("/bundles/{bundleId}", GetRolloutStatus)
            .WithName("GetRolloutStatus")
            .WithSummary("Get rollout status for bundle")
            .WithDescription("Retrieves the rollout status for all devices associated with a specific bundle, including statistics.");

        group.MapPut("/{rolloutId}/status", UpdateRolloutStatus)
            .WithName("UpdateRolloutStatus")
            .WithSummary("Update rollout status")
            .WithDescription("Updates the status of a specific rollout (e.g., mark as succeeded or failed).");

        return app;
    }

    private static async Task<IResult> GetRolloutStatus(
        string bundleId,
        GetRolloutStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRolloutStatusQuery(bundleId);
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

    private static async Task<IResult> UpdateRolloutStatus(
        Guid rolloutId,
        UpdateRolloutStatusCommand command,
        UpdateRolloutStatusHandler handler,
        CancellationToken cancellationToken)
    {
        // Override rolloutId from route
        var updatedCommand = command with { RolloutId = rolloutId };
        var result = await handler.Handle(updatedCommand, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }
}
