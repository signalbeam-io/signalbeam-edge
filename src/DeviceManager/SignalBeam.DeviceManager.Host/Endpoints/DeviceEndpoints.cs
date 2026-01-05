using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Infrastructure.Queries;
using SignalBeam.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

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

        group.MapGet("/{deviceId:guid}/registration-status", GetRegistrationStatus)
            .WithName("GetRegistrationStatus")
            .WithSummary("Get device registration status")
            .WithDescription("Retrieves the current registration status and API key (if approved) for a device.");

        group.MapGet("/", GetDevices)
            .WithName("GetDevices")
            .WithSummary("Get devices with filters")
            .WithDescription("Retrieves a paginated list of devices with optional filters.");

        group.MapGet("/by-status/{status}", GetDevicesByRegistrationStatus)
            .WithName("GetDevicesByRegistrationStatus")
            .WithSummary("Get devices by registration status")
            .WithDescription("Retrieves devices filtered by registration status (Pending, Approved, Rejected).");

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

        // Device registration approval and API key management
        group.MapPost("/{deviceId:guid}/approve", ApproveDeviceRegistration)
            .WithName("ApproveDeviceRegistration")
            .WithSummary("Approve device registration")
            .WithDescription("Approves a pending device registration and generates an API key.");

        group.MapPost("/{deviceId:guid}/reject", RejectDeviceRegistration)
            .WithName("RejectDeviceRegistration")
            .WithSummary("Reject device registration")
            .WithDescription("Rejects a pending device registration.");

        group.MapPost("/{deviceId:guid}/api-keys", GenerateDeviceApiKey)
            .WithName("GenerateDeviceApiKey")
            .WithSummary("Generate device API key")
            .WithDescription("Generates a new API key for a device (for key rotation).");

        group.MapDelete("/api-keys/{apiKeyId:guid}", RevokeDeviceApiKey)
            .WithName("RevokeDeviceApiKey")
            .WithSummary("Revoke device API key")
            .WithDescription("Revokes an existing device API key.");

        // Registration token management
        var tokenGroup = app.MapGroup("/api/registration-tokens")
            .WithTags("Registration Tokens")
            .WithOpenApi();

        tokenGroup.MapPost("/", GenerateRegistrationToken)
            .WithName("GenerateRegistrationToken")
            .WithSummary("Generate registration token")
            .WithDescription("Generates a new registration token for device registration (admin only).");

        tokenGroup.MapGet("/", GetRegistrationTokens)
            .WithName("GetRegistrationTokens")
            .WithSummary("Get registration tokens")
            .WithDescription("Retrieves a paginated list of registration tokens for a tenant.");

        tokenGroup.MapDelete("/{tokenId:guid}", RevokeRegistrationToken)
            .WithName("RevokeRegistrationToken")
            .WithSummary("Revoke registration token")
            .WithDescription("Revokes a registration token, preventing further use.");

        // Authentication logs - TODO: Implement
        // var authLogsGroup = app.MapGroup("/api/authentication-logs")
        //     .WithTags("Authentication Logs")
        //     .WithOpenApi();

        // authLogsGroup.MapGet("/", GetAuthenticationLogs)
        //     .WithName("GetAuthenticationLogs")
        //     .WithSummary("Get authentication logs")
        //     .WithDescription("Retrieves authentication logs with optional filtering by device, date range, and success status.");

        return app;
    }

    private static async Task<IResult> RegisterDevice(
        RegisterDeviceCommand command,
        [FromServices] RegisterDeviceHandler handler,
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
        [FromServices] GetDevicesHandler handler,
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
        [FromServices] GetDeviceByIdHandler handler,
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
        [FromServices] UpdateDeviceHandler handler,
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
        [FromServices] AddDeviceTagHandler handler,
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
        [FromServices] RecordHeartbeatHandler handler,
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
        [FromServices] AssignDeviceToGroupHandler handler,
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
        [FromServices] ReportDeviceStateHandler handler,
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
        [FromServices] GetDeviceHealthHandler handler,
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
        [FromServices] GetDevicesByGroupHandler handler,
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
        [FromServices] UpdateDeviceMetricsHandler handler,
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
        [FromServices] GetDeviceActivityLogHandler handler,
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
        [FromServices] GetDeviceMetricsHandler handler,
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

    private static async Task<IResult> ApproveDeviceRegistration(
        Guid deviceId,
        [FromServices] ApproveDeviceRegistrationHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ApproveDeviceRegistrationCommand(deviceId);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new
            {
                deviceId = result.Value!.DeviceId,
                apiKey = result.Value.ApiKey,
                expiresAt = result.Value.ExpiresAt,
                message = "Device registration approved. Save the API key - it will not be shown again."
            })
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> RejectDeviceRegistration(
        Guid deviceId,
        RejectDeviceRegistrationCommand command,
        [FromServices] RejectDeviceRegistrationHandler handler,
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

    private static async Task<IResult> GenerateDeviceApiKey(
        Guid deviceId,
        GenerateDeviceApiKeyCommand command,
        [FromServices] GenerateDeviceApiKeyHandler handler,
        CancellationToken cancellationToken)
    {
        // Override deviceId from route
        var updatedCommand = command with { DeviceId = deviceId };
        var result = await handler.Handle(updatedCommand, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new
            {
                deviceId = result.Value!.DeviceId,
                apiKey = result.Value.ApiKey,
                keyPrefix = result.Value.KeyPrefix,
                createdAt = result.Value.CreatedAt,
                expiresAt = result.Value.ExpiresAt,
                message = "API key generated successfully. Save the key - it will not be shown again."
            })
            : Results.BadRequest(new
            {
                error = result.Error!.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString()
            });
    }

    private static async Task<IResult> RevokeDeviceApiKey(
        Guid apiKeyId,
        [FromServices] RevokeDeviceApiKeyHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RevokeDeviceApiKeyCommand(apiKeyId);
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

    private static async Task<IResult> GetRegistrationStatus(
        Guid deviceId,
        [FromServices] GetRegistrationStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRegistrationStatusQuery(deviceId);
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

    private static async Task<IResult> GenerateRegistrationToken(
        GenerateRegistrationTokenCommand command,
        [FromServices] GenerateRegistrationTokenHandler handler,
        CancellationToken cancellationToken)
    {
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

    private static async Task<IResult> GetDevicesByRegistrationStatus(
        string status,
        Guid tenantId,
        [FromServices] GetDevicesByRegistrationStatusHandler handler,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<DeviceRegistrationStatus>(status, ignoreCase: true, out var registrationStatus))
        {
            return Results.BadRequest(new
            {
                error = "InvalidStatus",
                message = $"Invalid registration status: {status}. Valid values: Pending, Approved, Rejected"
            });
        }

        var query = new GetDevicesByRegistrationStatusQuery(
            tenantId,
            registrationStatus,
            pageNumber,
            pageSize);

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

    // TODO: Implement authentication logs
    // private static async Task<IResult> GetAuthenticationLogs(
    //     [FromServices] GetAuthenticationLogsHandler handler,
    //     Guid? deviceId = null,
    //     DateTimeOffset? startDate = null,
    //     DateTimeOffset? endDate = null,
    //     bool? successOnly = null,
    //     int pageNumber = 1,
    //     int pageSize = 50,
    //     CancellationToken cancellationToken = default)
    // {
    //     var query = new GetAuthenticationLogsQuery(
    //         deviceId,
    //         startDate,
    //         endDate,
    //         successOnly,
    //         pageNumber,
    //         pageSize);
    //
    //     var result = await handler.Handle(query, cancellationToken);
    //
    //     return result.IsSuccess
    //         ? Results.Ok(result.Value)
    //         : Results.BadRequest(new
    //         {
    //             error = result.Error!.Code,
    //             message = result.Error.Message,
    //             type = result.Error.Type.ToString()
    //         });
    // }

    private static async Task<IResult> GetRegistrationTokens(
        [AsParameters] GetRegistrationTokensQuery query,
        [FromServices] GetRegistrationTokensHandler handler,
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

    private static async Task<IResult> RevokeRegistrationToken(
        Guid tokenId,
        [FromServices] RevokeRegistrationTokenHandler handler,
        string? revokedBy = null,
        CancellationToken cancellationToken = default)
    {
        var command = new RevokeRegistrationTokenCommand(tokenId, revokedBy);
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
}
