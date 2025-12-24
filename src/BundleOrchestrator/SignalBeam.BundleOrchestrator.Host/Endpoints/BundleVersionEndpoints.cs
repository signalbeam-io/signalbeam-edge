using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Storage;
using SignalBeam.Domain.ValueObjects;

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
            .WithDescription("Creates a new version of an existing bundle using a bundle definition payload.");

        group.MapGet("/latest", GetLatestBundleDefinition)
            .WithName("GetLatestBundleDefinition")
            .WithSummary("Get latest bundle definition")
            .WithDescription("Retrieves the latest published bundle definition.");

        group.MapGet("/{version}/download", DownloadBundleDefinition)
            .WithName("DownloadBundleDefinition")
            .WithSummary("Download bundle definition")
            .WithDescription("Returns a time-limited download URL for the bundle definition.");

        group.MapGet("/{version}", GetBundleVersion)
            .WithName("GetBundleVersion")
            .WithSummary("Get bundle definition")
            .WithDescription("Retrieves the bundle definition for a specific version.");

        return app;
    }

    private static async Task<IResult> CreateBundleVersion(
        string bundleId,
        BundleDefinition definition,
        UploadBundleVersionHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UploadBundleVersionCommand(bundleId, definition);
        var result = await handler.Handle(command, cancellationToken);

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
        HttpRequest request,
        HttpResponse response,
        GetBundleDefinitionHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetBundleDefinitionQuery(bundleId, version);
        var result = await handler.Handle(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.NotFound(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
        }

        if (TryHandleBundleDefinitionCaching(request, response, result.Value!.Checksum, result.Value.CreatedAt, out var cached))
        {
            return cached!;
        }

        return Results.Ok(result.Value.Definition);
    }

    private static async Task<IResult> GetLatestBundleDefinition(
        string bundleId,
        HttpRequest request,
        HttpResponse response,
        GetLatestBundleDefinitionHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetLatestBundleDefinitionQuery(bundleId);
        var result = await handler.Handle(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.NotFound(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
        }

        if (TryHandleBundleDefinitionCaching(request, response, result.Value!.Checksum, result.Value.CreatedAt, out var cached))
        {
            return cached!;
        }

        return Results.Ok(result.Value.Definition);
    }

    private static async Task<IResult> DownloadBundleDefinition(
        string bundleId,
        string version,
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IBundleStorageService bundleStorageService,
        CancellationToken cancellationToken)
    {
        if (!BundleId.TryParse(bundleId, out var parsedBundleId))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_BUNDLE_ID",
                message = $"Invalid bundle ID format: {bundleId}"
            });
        }

        if (!BundleVersion.TryParse(version, out var bundleVersion) || bundleVersion is null)
        {
            return Results.BadRequest(new
            {
                error = "INVALID_VERSION",
                message = $"Invalid semantic version format: {version}"
            });
        }

        var bundle = await bundleRepository.GetByIdAsync(parsedBundleId, cancellationToken);
        if (bundle is null)
        {
            return Results.NotFound(new
            {
                error = "BUNDLE_NOT_FOUND",
                message = $"Bundle with ID {bundleId} not found."
            });
        }

        var versionEntity = await bundleVersionRepository.GetByBundleAndVersionAsync(
            parsedBundleId,
            bundleVersion,
            cancellationToken);

        if (versionEntity is null)
        {
            return Results.NotFound(new
            {
                error = "VERSION_NOT_FOUND",
                message = $"Version {version} not found for bundle {bundleId}."
            });
        }

        var downloadUrl = await bundleStorageService.GenerateBundleDownloadUrlAsync(
            bundle.TenantId.Value.ToString(),
            bundleId,
            bundleVersion.ToString(),
            TimeSpan.FromHours(1),
            cancellationToken);

        return Results.Redirect(downloadUrl);
    }

    internal static bool TryHandleBundleDefinitionCaching(
        HttpRequest request,
        HttpResponse response,
        string checksum,
        DateTimeOffset createdAt,
        out IResult? result)
    {
        var etag = $"\"{checksum}\"";

        if (request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) &&
            ifNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            response.Headers["ETag"] = etag;
            result = Results.StatusCode(StatusCodes.Status304NotModified);
            return true;
        }

        response.Headers["ETag"] = etag;
        response.Headers["Cache-Control"] = "public, max-age=300";
        response.Headers["Last-Modified"] = createdAt.ToUniversalTime().ToString("R");
        result = null;
        return false;
    }
}
