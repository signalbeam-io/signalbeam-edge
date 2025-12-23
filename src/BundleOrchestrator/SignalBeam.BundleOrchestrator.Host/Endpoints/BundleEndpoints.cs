using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Bundle API endpoints.
/// </summary>
public static class BundleEndpoints
{
    /// <summary>
    /// Maps all bundle-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapBundleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bundles")
            .WithTags("Bundles")
            .WithOpenApi();

        group.MapPost("/", CreateBundle)
            .WithName("CreateBundle")
            .WithSummary("Create a new app bundle")
            .WithDescription("Creates a new app bundle with the specified name and description.");

        group.MapGet("/", GetBundles)
            .WithName("GetBundles")
            .WithSummary("Get all bundles")
            .WithDescription("Retrieves all app bundles for the specified tenant.");

        group.MapGet("/{bundleId}", GetBundleById)
            .WithName("GetBundleById")
            .WithSummary("Get bundle by ID")
            .WithDescription("Retrieves detailed information about a specific bundle including all its versions.");

        group.MapGet("/{bundleId}/assigned-devices", GetBundleAssignedDevices)
            .WithName("GetBundleAssignedDevices")
            .WithSummary("Get devices assigned to a bundle")
            .WithDescription("Retrieves all devices that have this bundle assigned.");

        return app;
    }

    private static async Task<IResult> CreateBundle(
        CreateBundleCommand command,
        CreateBundleHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/bundles/{result.Value!.BundleId}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> GetBundles(
        Guid tenantId,
        GetBundlesHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundlesQuery(tenantId);
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

    private static async Task<IResult> GetBundleById(
        string bundleId,
        GetBundleByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundleByIdQuery(bundleId);
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

    private static async Task<IResult> GetBundleAssignedDevices(
        string bundleId,
        GetBundleAssignedDevicesHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundleAssignedDevicesQuery(bundleId);
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
