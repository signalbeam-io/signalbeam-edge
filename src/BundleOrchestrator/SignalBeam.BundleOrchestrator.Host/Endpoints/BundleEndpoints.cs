using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Request model for creating a bundle from the UI.
/// </summary>
public record CreateBundleRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Version { get; init; }
    public List<ContainerDefinitionRequest>? Containers { get; init; }
}

/// <summary>
/// Container definition in the create bundle request.
/// </summary>
public record ContainerDefinitionRequest
{
    public string Name { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public Dictionary<string, string>? Environment { get; init; }
    public List<PortMappingDto>? Ports { get; init; }
    public List<VolumeMountDto>? Volumes { get; init; }
}

/// <summary>
/// Port mapping DTO.
/// </summary>
public record PortMappingDto
{
    public int Host { get; init; }
    public int Container { get; init; }
    public string Protocol { get; init; } = "tcp";
}

/// <summary>
/// Volume mount DTO.
/// </summary>
public record VolumeMountDto
{
    public string HostPath { get; init; } = string.Empty;
    public string ContainerPath { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
}

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
            .WithSummary("Create a new bundle")
            .WithDescription("Creates a new bundle with a name and optional description. Optionally creates an initial version with containers.");

        group.MapPost("/upload", UploadBundle)
            .WithName("UploadBundle")
            .WithSummary("Upload a complete bundle definition")
            .WithDescription("Uploads a complete bundle definition, stores it, and creates the bundle with its initial version.");

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
        CreateBundleRequest request,
        Guid? tenantId,
        HttpContext context,
        CreateBundleHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_TENANT_ID",
                message = "Tenant ID is required."
            });
        }

        // Map ContainerDefinitionRequest to ContainerSpecDto
        // Note: ContainerSpecDto expects simple List<string> for Ports, but we'll serialize it
        var containers = request.Containers?.Select(c =>
        {
            // Convert port mappings to the format expected by ContainerSpec
            var ports = c.Ports?.Select(p => $"{p.Host}:{p.Container}/{p.Protocol}").ToList();

            return new ContainerSpecDto(
                c.Name,
                $"{c.Image}:{c.Tag ?? "latest"}",
                c.Environment,
                ports
            );
        }).ToList();

        var command = new CreateBundleCommand(
            resolvedTenantId,
            request.Name,
            request.Description,
            request.Version,
            containers);

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

    private static async Task<IResult> UploadBundle(
        BundleDefinition definition,
        Guid? tenantId,
        HttpContext context,
        UploadBundleHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_TENANT_ID",
                message = "Tenant ID is required."
            });
        }

        var command = new UploadBundleCommand(resolvedTenantId, definition);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/bundles/{result.Value!.BundleId}/versions/{result.Value.Version}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static bool TryResolveTenantId(Guid? tenantId, HttpContext context, out Guid resolvedTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            resolvedTenantId = tenantId.Value;
            return true;
        }

        var tenantIdClaim = context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value;
        return Guid.TryParse(tenantIdClaim, out resolvedTenantId);
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
