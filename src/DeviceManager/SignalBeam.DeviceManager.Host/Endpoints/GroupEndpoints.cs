using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;

namespace SignalBeam.DeviceManager.Host.Endpoints;

/// <summary>
/// Device Group API endpoints.
/// </summary>
public static class GroupEndpoints
{
    /// <summary>
    /// Maps all device group-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/groups")
            .WithTags("Device Groups")
            .WithOpenApi();

        group.MapGet("/", GetDeviceGroups)
            .WithName("GetDeviceGroups")
            .WithSummary("Get device groups")
            .WithDescription("Retrieves all device groups for a tenant.");

        group.MapPost("/", CreateDeviceGroup)
            .WithName("CreateDeviceGroup")
            .WithSummary("Create device group")
            .WithDescription("Creates a new device group for organizing devices.");

        group.MapPost("/{groupId:guid}/devices", AddDeviceToGroup)
            .WithName("AddDeviceToGroup")
            .WithSummary("Add device to group")
            .WithDescription("Adds a device to the specified device group.");

        return app;
    }

    private static async Task<IResult> GetDeviceGroups(
        [AsParameters] GetDeviceGroupsQuery query,
        GetDeviceGroupsHandler handler,
        CancellationToken cancellationToken)
    {
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

    private static async Task<IResult> CreateDeviceGroup(
        CreateDeviceGroupCommand command,
        CreateDeviceGroupHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/groups/{result.Value!.DeviceGroupId}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> AddDeviceToGroup(
        Guid groupId,
        AddDeviceToGroupRequest request,
        AssignDeviceToGroupHandler handler,
        CancellationToken cancellationToken)
    {
        // Use the existing AssignDeviceToGroup command with the groupId from route
        var command = new AssignDeviceToGroupCommand(
            DeviceId: request.DeviceId,
            DeviceGroupId: groupId);

        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    public record AddDeviceToGroupRequest(Guid DeviceId);
}
