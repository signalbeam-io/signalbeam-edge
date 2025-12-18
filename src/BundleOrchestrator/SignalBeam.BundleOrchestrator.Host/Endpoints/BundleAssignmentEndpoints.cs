using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;

namespace SignalBeam.BundleOrchestrator.Host.Endpoints;

/// <summary>
/// Bundle Assignment API endpoints.
/// </summary>
public static class BundleAssignmentEndpoints
{
    /// <summary>
    /// Maps all bundle assignment-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapBundleAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var deviceGroup = app.MapGroup("/api/devices")
            .WithTags("Bundle Assignments")
            .WithOpenApi();

        deviceGroup.MapPost("/{deviceId}/bundle", AssignBundleToDevice)
            .WithName("AssignBundleToDevice")
            .WithSummary("Assign bundle to device")
            .WithDescription("Assigns a specific bundle version to a device, updating its desired state.");

        deviceGroup.MapGet("/{deviceId}/desired-state", GetDeviceDesiredState)
            .WithName("GetDeviceDesiredState")
            .WithSummary("Get device desired state")
            .WithDescription("Retrieves the desired bundle state for a specific device.");

        var groupGroup = app.MapGroup("/api/device-groups")
            .WithTags("Bundle Assignments")
            .WithOpenApi();

        groupGroup.MapPost("/{deviceGroupId}/bundle", AssignBundleToGroup)
            .WithName("AssignBundleToGroup")
            .WithSummary("Assign bundle to device group")
            .WithDescription("Assigns a specific bundle version to all devices in a group.");

        return app;
    }

    private static async Task<IResult> AssignBundleToDevice(
        string deviceId,
        AssignBundleToDeviceCommand command,
        AssignBundleToDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        // Override deviceId from route
        var updatedCommand = command with { DeviceId = deviceId };
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

    private static async Task<IResult> GetDeviceDesiredState(
        string deviceId,
        GetDeviceDesiredStateHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetDeviceDesiredStateQuery(deviceId);
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

    private static async Task<IResult> AssignBundleToGroup(
        string deviceGroupId,
        AssignBundleToGroupCommand command,
        AssignBundleToGroupHandler handler,
        CancellationToken cancellationToken)
    {
        // Override deviceGroupId from route
        var updatedCommand = command with { DeviceGroupId = deviceGroupId };
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
