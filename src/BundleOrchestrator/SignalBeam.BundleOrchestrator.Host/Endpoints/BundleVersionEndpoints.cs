using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Bundle Version API endpoints.
/// </summary>
public static class BundleVersionEndpoints
{
    /// <summary>
    /// Maps all bundle version-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapBundleVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bundles/{bundleId}/versions")
            .WithTags("Bundle Versions")
            .WithOpenApi();

        group.MapPost("/", CreateBundleVersion)
            .WithName("CreateBundleVersion")
            .WithSummary("Create a new bundle version")
            .WithDescription("Creates a new version of an existing bundle with container specifications.");

        group.MapGet("/{version}", GetBundleVersion)
            .WithName("GetBundleVersion")
            .WithSummary("Get bundle version details")
            .WithDescription("Retrieves detailed information about a specific version of a bundle including all container specifications.");

        return app;
    }

    private static async Task<IResult> CreateBundleVersion(
        string bundleId,
        CreateBundleVersionCommand command,
        CreateBundleVersionHandler handler,
        CancellationToken cancellationToken)
    {
        // Override bundleId from route
        var updatedCommand = command with { BundleId = bundleId };
        var result = await handler.Handle(updatedCommand, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/bundles/{bundleId}/versions/{result.Value!.Version}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> GetBundleVersion(
        string bundleId,
        string version,
        GetBundleVersionHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundleVersionQuery(bundleId, version);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }
}
