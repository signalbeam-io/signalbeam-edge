using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.Domain.Enums;

namespace SignalBeam.DeviceManager.Host.Endpoints;

/// <summary>
/// Device API endpoints.
/// </summary>
public static class DeviceEndpoints
{
    /// <summary>
    /// Maps all device-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices")
            .WithTags("Devices")
            .WithOpenApi();

        group.MapPost("/", RegisterDevice)
            .WithName("RegisterDevice")
            .WithSummary("Register a new device")
            .WithDescription("Registers a new device in the system with the specified tenant and metadata.");

        group.MapGet("/", GetDevices)
            .WithName("GetDevices")
            .WithSummary("Get devices with filters")
            .WithDescription("Retrieves a paginated list of devices with optional filters.");

        group.MapGet("/{deviceId:guid}", GetDeviceById)
            .WithName("GetDeviceById")
            .WithSummary("Get device by ID")
            .WithDescription("Retrieves detailed information about a specific device.");

        group.MapPut("/{deviceId:guid}", UpdateDevice)
            .WithName("UpdateDevice")
            .WithSummary("Update device")
            .WithDescription("Updates device name and/or metadata.");

        group.MapPost("/{deviceId:guid}/tags", AddDeviceTag)
            .WithName("AddDeviceTag")
            .WithSummary("Add tag to device")
            .WithDescription("Adds a tag to a device for categorization.");

        group.MapPost("/{deviceId:guid}/heartbeat", RecordHeartbeat)
            .WithName("RecordHeartbeat")
            .WithSummary("Record device heartbeat")
            .WithDescription("Records a heartbeat from a device to update its online status.");

        group.MapPut("/{deviceId:guid}/group", AssignDeviceToGroup)
            .WithName("AssignDeviceToGroup")
            .WithSummary("Assign device to group")
            .WithDescription("Assigns a device to a device group or removes it from a group.");

        group.MapPost("/{deviceId:guid}/state", ReportDeviceState)
            .WithName("ReportDeviceState")
            .WithSummary("Report device state")
            .WithDescription("Reports device state from the edge agent, including bundle deployment status.");

        group.MapGet("/{deviceId:guid}/health", GetDeviceHealth)
            .WithName("GetDeviceHealth")
            .WithSummary("Get device health")
            .WithDescription("Retrieves health information about a specific device.");

        group.MapGet("/groups/{deviceGroupId:guid}", GetDevicesByGroup)
            .WithName("GetDevicesByGroup")
            .WithSummary("Get devices by group")
            .WithDescription("Retrieves all devices in a specific device group with optional filters.");

        group.MapPost("/{deviceId:guid}/metrics", UpdateDeviceMetrics)
            .WithName("UpdateDeviceMetrics")
            .WithSummary("Update device metrics")
            .WithDescription("Records device metrics (CPU, memory, disk usage, etc.) for monitoring.");

        group.MapGet("/{deviceId:guid}/metrics", GetDeviceMetrics)
            .WithName("GetDeviceMetrics")
            .WithSummary("Get device metrics history")
            .WithDescription("Retrieves historical metrics data for a device with optional date range filtering.");

        group.MapGet("/{deviceId:guid}/activity-log", GetDeviceActivityLog)
            .WithName("GetDeviceActivityLog")
            .WithSummary("Get device activity log")
            .WithDescription("Retrieves the activity log for a specific device.");

        return app;
    }

    private static async Task<IResult> RegisterDevice(
        RegisterDeviceCommand command,
        RegisterDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/devices/{result.Value!.DeviceId}", result.Value)
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> GetDevices(
        [AsParameters] GetDevicesQuery query,
        GetDevicesHandler handler,
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

    private static async Task<IResult> GetDeviceById(
        Guid deviceId,
        GetDeviceByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetDeviceByIdQuery(deviceId);
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

    private static async Task<IResult> UpdateDevice(
        Guid deviceId,
        UpdateDeviceCommand command,
        UpdateDeviceHandler handler,
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

    private static async Task<IResult> AddDeviceTag(
        Guid deviceId,
        AddDeviceTagCommand command,
        AddDeviceTagHandler handler,
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

    private static async Task<IResult> RecordHeartbeat(
        Guid deviceId,
        RecordHeartbeatCommand command,
        RecordHeartbeatHandler handler,
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

    private static async Task<IResult> AssignDeviceToGroup(
        Guid deviceId,
        AssignDeviceToGroupCommand command,
        AssignDeviceToGroupHandler handler,
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

    private static async Task<IResult> ReportDeviceState(
        Guid deviceId,
        ReportDeviceStateCommand command,
        ReportDeviceStateHandler handler,
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

    private static async Task<IResult> GetDeviceHealth(
        Guid deviceId,
        GetDeviceHealthHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetDeviceHealthQuery(deviceId);
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

    private static async Task<IResult> GetDevicesByGroup(
        [AsParameters] GetDevicesByGroupQuery query,
        GetDevicesByGroupHandler handler,
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

    private static async Task<IResult> UpdateDeviceMetrics(
        Guid deviceId,
        UpdateDeviceMetricsCommand command,
        UpdateDeviceMetricsHandler handler,
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

    private static async Task<IResult> GetDeviceActivityLog(
        [AsParameters] GetDeviceActivityLogQuery query,
        GetDeviceActivityLogHandler handler,
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

    private static async Task<IResult> GetDeviceMetrics(
        [AsParameters] GetDeviceMetricsQuery query,
        GetDeviceMetricsHandler handler,
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
}
