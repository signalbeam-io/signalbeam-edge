using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// API endpoints for phased rollout management.
/// </summary>
public static class PhasedRolloutEndpoints
{
    /// <summary>
    /// Maps all phased rollout-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapPhasedRolloutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/phased-rollouts")
            .WithTags("Phased Rollouts")
            .WithOpenApi();

        // Query endpoints
        group.MapGet("", ListPhasedRollouts)
            .WithName("ListPhasedRollouts")
            .WithSummary("List phased rollouts with pagination")
            .WithDescription("Retrieves paginated list of phased rollouts with optional filtering by status and bundle.");

        group.MapGet("/{rolloutId}", GetPhasedRolloutDetails)
            .WithName("GetPhasedRolloutDetails")
            .WithSummary("Get detailed phased rollout information")
            .WithDescription("Retrieves complete rollout details including all phases and device assignments.");

        group.MapGet("/active", GetActiveRollouts)
            .WithName("GetActiveRollouts")
            .WithSummary("Get active rollouts")
            .WithDescription("Retrieves all active (InProgress or Paused) rollouts for monitoring.");

        group.MapGet("/bundles/{bundleId}/history", GetBundleRolloutHistory)
            .WithName("GetBundleRolloutHistory")
            .WithSummary("Get rollout history for a bundle")
            .WithDescription("Retrieves complete rollout history for a specific bundle.");

        // Command endpoints
        group.MapPost("", CreatePhasedRollout)
            .WithName("CreatePhasedRollout")
            .WithSummary("Create a new phased rollout")
            .WithDescription("Creates a phased rollout with multiple deployment phases.");

        group.MapPost("/{rolloutId}/start", StartRollout)
            .WithName("StartRollout")
            .WithSummary("Start a pending rollout")
            .WithDescription("Starts a rollout and activates the first phase.");

        group.MapPost("/{rolloutId}/pause", PauseRollout)
            .WithName("PauseRollout")
            .WithSummary("Pause an in-progress rollout")
            .WithDescription("Pauses an active rollout, preventing further phase progression.");

        group.MapPost("/{rolloutId}/resume", ResumeRollout)
            .WithName("ResumeRollout")
            .WithSummary("Resume a paused rollout")
            .WithDescription("Resumes a paused rollout from the current phase.");

        group.MapPost("/{rolloutId}/advance", AdvancePhase)
            .WithName("AdvancePhase")
            .WithSummary("Manually advance to next phase")
            .WithDescription("Completes current phase and advances to the next phase.");

        group.MapPost("/{rolloutId}/rollback", RollbackRollout)
            .WithName("RollbackRollout")
            .WithSummary("Rollback rollout to previous version")
            .WithDescription("Rolls back all devices to the previous bundle version.");

        return app;
    }

    // Query endpoint handlers

    private static async Task<IResult> ListPhasedRollouts(
        Guid tenantId,
        string? status,
        string? bundleId,
        int page,
        int pageSize,
        ListPhasedRolloutsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new ListPhasedRolloutsQuery(tenantId, status, bundleId, page, pageSize);
        var result = await handler.Handle(query, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> GetPhasedRolloutDetails(
        Guid rolloutId,
        GetPhasedRolloutDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetPhasedRolloutDetailsQuery(rolloutId);
        var result = await handler.Handle(query, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> GetActiveRollouts(
        Guid tenantId,
        GetActiveRolloutsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetActiveRolloutsQuery(tenantId);
        var result = await handler.Handle(query, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> GetBundleRolloutHistory(
        string bundleId,
        GetBundleRolloutHistoryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundleRolloutHistoryQuery(bundleId);
        var result = await handler.Handle(query, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    // Command endpoint handlers

    private static async Task<IResult> CreatePhasedRollout(
        CreatePhasedRolloutCommand command,
        CreatePhasedRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Created($"/api/phased-rollouts/{success.RolloutId}", success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> StartRollout(
        Guid rolloutId,
        StartRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new StartRolloutCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> PauseRollout(
        Guid rolloutId,
        PauseRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new PauseRolloutCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> ResumeRollout(
        Guid rolloutId,
        ResumeRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ResumeRolloutCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> AdvancePhase(
        Guid rolloutId,
        AdvancePhaseHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new AdvancePhaseCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    private static async Task<IResult> RollbackRollout(
        Guid rolloutId,
        RollbackRolloutHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RollbackRolloutCommand(rolloutId);
        var result = await handler.Handle(command, cancellationToken);

        return result.Match(
            success => Results.Ok(success),
            error => MapErrorToResult(error));
    }

    // Helper method to map errors to HTTP results
    private static IResult MapErrorToResult(Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new
            {
                error = error.Code,
                message = error.Message,
                type = error.Type.ToString()
            }),
            ErrorType.Validation => Results.BadRequest(new
            {
                error = error.Code,
                message = error.Message,
                type = error.Type.ToString()
            }),
            ErrorType.Conflict => Results.Conflict(new
            {
                error = error.Code,
                message = error.Message,
                type = error.Type.ToString()
            }),
            ErrorType.Forbidden => Results.Forbid(),
            _ => Results.Problem(
                title: "Internal Server Error",
                detail: error.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?>
                {
                    ["error"] = error.Code,
                    ["type"] = error.Type.ToString()
                })
        };
    }
}
