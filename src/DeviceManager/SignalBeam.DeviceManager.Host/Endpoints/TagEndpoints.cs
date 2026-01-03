using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace SignalBeam.DeviceManager.Host.Endpoints;

/// <summary>
/// Tag API endpoints.
/// </summary>
public static class TagEndpoints
{
    /// <summary>
    /// Maps all tag-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags")
            .WithTags("Tags")
            .WithOpenApi();

        group.MapGet("/", GetAllTags)
            .WithName("GetAllTags")
            .WithSummary("Get all tags")
            .WithDescription("Retrieves all unique tags across devices with usage counts.");

        group.MapDelete("/{deviceId:guid}/tags/{tag}", RemoveDeviceTag)
            .WithName("RemoveDeviceTag")
            .WithSummary("Remove tag from device")
            .WithDescription("Removes a specific tag from a device.");

        var searchGroup = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .WithOpenApi();

        searchGroup.MapGet("/search", SearchDevicesByTagQuery)
            .WithName("SearchDevicesByTagQuery")
            .WithSummary("Search devices by tag query")
            .WithDescription("Search devices using tag query expressions (e.g., 'environment=production AND location=warehouse-*').");

        return app;
    }

    private static async Task<IResult> GetAllTags(
        [FromQuery] Guid tenantId,
        [FromServices] GetAllTagsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllTagsQuery(tenantId);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> RemoveDeviceTag(
        Guid deviceId,
        string tag,
        [FromServices] RemoveDeviceTagHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RemoveDeviceTagCommand(deviceId, tag);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_NOT_FOUND" => Results.NotFound(result.Error),
                _ => Results.BadRequest(result.Error)
            };
    }

    private static async Task<IResult> SearchDevicesByTagQuery(
        [FromQuery] Guid tenantId,
        [FromQuery] string query,
        [FromServices] GetDevicesByTagQueryHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var searchQuery = new GetDevicesByTagQueryQuery(tenantId, query, pageNumber, pageSize);
        var result = await handler.Handle(searchQuery, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "INVALID_TAG_QUERY_SYNTAX" or "INVALID_TAG_QUERY_FORMAT" => Results.BadRequest(result.Error),
                _ => Results.BadRequest(result.Error)
            };
    }
}
