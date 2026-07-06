using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        // Device-group management is entirely an operator concern — devices never call it.
        var group = app.MapGroup("/api/groups")
            .WithTags("Device Groups")
            .RequireAuthorization(AuthorizationPolicies.OperatorAccess);

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

        group.MapPut("/{groupId:guid}", UpdateDeviceGroup)
            .WithName("UpdateDeviceGroup")
            .WithSummary("Update device group")
            .WithDescription("Updates a device group's name, description, or tag query.");

        group.MapDelete("/{groupId:guid}/devices/{deviceId:guid}", RemoveDeviceFromGroup)
            .WithName("RemoveDeviceFromGroup")
            .WithSummary("Remove device from group")
            .WithDescription("Removes a device from a static device group.");

        group.MapGet("/{groupId:guid}/memberships", GetGroupMemberships)
            .WithName("GetGroupMemberships")
            .WithSummary("Get group memberships")
            .WithDescription("Retrieves all memberships (devices) for a specific group.");

        group.MapPost("/{groupId:guid}/bulk/add-tags", BulkAddDeviceTags)
            .WithName("BulkAddDeviceTags")
            .WithSummary("Bulk add tags to group devices")
            .WithDescription("Adds a tag to all devices in a device group.");

        group.MapPost("/{groupId:guid}/bulk/remove-tags", BulkRemoveDeviceTags)
            .WithName("BulkRemoveDeviceTags")
            .WithSummary("Bulk remove tags from group devices")
            .WithDescription("Removes a tag from all devices in a device group.");

        return app;
    }

    private static async Task<IResult> GetDeviceGroups(
        HttpContext context,
        [FromServices] GetDeviceGroupsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        // Resolve tenant from the explicit query value or the authenticated context. Binding a
        // required non-nullable Guid from the query string would 400 before the handler runs.
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_TENANT_ID",
                message = "Tenant ID could not be resolved from the request or authentication context.",
                type = "Validation"
            });
        }

        var query = new GetDeviceGroupsQuery(resolvedTenantId);
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
        [FromServices] CreateDeviceGroupHandler handler,
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
        HttpContext context,
        [FromServices] AddDeviceToGroupHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_TENANT_ID",
                message = "Tenant ID could not be resolved from the request or authentication context.",
                type = "Validation"
            });
        }

        // Add the device to the static group's membership table (the source of truth that the
        // bulk-tag and membership reads expect). Assigning Device.DeviceGroupId alone is not enough.
        var command = new AddDeviceToGroupCommand(resolvedTenantId, groupId, request.DeviceId);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_ACCESS_DENIED" or "DEVICE_ACCESS_DENIED" => Results.Forbid(),
                "MEMBERSHIP_ALREADY_EXISTS" => Results.Conflict(result.Error),
                // DEVICE_GROUP_NOT_FOUND / DEVICE_NOT_FOUND / INVALID_GROUP_TYPE -> 400
                _ => Results.BadRequest(new
                {
                    error = result.Error.Code,
                    message = result.Error.Message,
                    type = result.Error.Type.ToString()
                })
            };
    }

    /// <summary>
    /// Resolves the tenant id from an explicit query value, falling back to the authenticated
    /// tenant claim set by the auth middleware.
    /// </summary>
    private static bool TryResolveTenantId(Guid? tenantId, HttpContext context, out Guid resolvedTenantId)
    {
        if (tenantId is { } value && value != Guid.Empty)
        {
            resolvedTenantId = value;
            return true;
        }

        var tenantIdClaim = context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value;
        return Guid.TryParse(tenantIdClaim, out resolvedTenantId) && resolvedTenantId != Guid.Empty;
    }

    private static async Task<IResult> UpdateDeviceGroup(
        Guid groupId,
        [FromQuery] Guid tenantId,
        UpdateDeviceGroupRequest request,
        [FromServices] UpdateDeviceGroupHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDeviceGroupCommand(
            tenantId,
            groupId,
            request.Name,
            request.Description,
            request.TagQuery);

        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_NOT_FOUND" => Results.NotFound(result.Error),
                "DEVICE_GROUP_ACCESS_DENIED" => Results.Forbid(),
                "DEVICE_GROUP_NAME_EXISTS" => Results.Conflict(result.Error),
                "INVALID_GROUP_TYPE" or "INVALID_TAG_QUERY" => Results.BadRequest(result.Error),
                _ => Results.BadRequest(result.Error)
            };
    }

    private static async Task<IResult> RemoveDeviceFromGroup(
        Guid groupId,
        Guid deviceId,
        [FromQuery] Guid tenantId,
        [FromServices] RemoveDeviceFromGroupHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RemoveDeviceFromGroupCommand(tenantId, groupId, deviceId);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_NOT_FOUND" or "DEVICE_NOT_FOUND" or "MEMBERSHIP_NOT_FOUND" => Results.NotFound(result.Error),
                "DEVICE_GROUP_ACCESS_DENIED" or "DEVICE_ACCESS_DENIED" => Results.Forbid(),
                "INVALID_GROUP_TYPE" or "INVALID_MEMBERSHIP_TYPE" => Results.BadRequest(result.Error),
                _ => Results.BadRequest(result.Error)
            };
    }

    private static async Task<IResult> GetGroupMemberships(
        Guid groupId,
        [FromQuery] Guid tenantId,
        [FromServices] GetGroupMembershipsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetGroupMembershipsQuery(tenantId, groupId);
        var result = await handler.Handle(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_NOT_FOUND" => Results.NotFound(result.Error),
                "DEVICE_GROUP_ACCESS_DENIED" => Results.Forbid(),
                _ => Results.BadRequest(result.Error)
            };
    }

    private static async Task<IResult> BulkAddDeviceTags(
        Guid groupId,
        [FromQuery] Guid tenantId,
        BulkTagRequest request,
        [FromServices] BulkAddDeviceTagsHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new BulkAddDeviceTagsCommand(tenantId, groupId, request.Tag);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_NOT_FOUND" => Results.NotFound(result.Error),
                "DEVICE_GROUP_ACCESS_DENIED" => Results.Forbid(),
                "INVALID_TAG_FORMAT" => Results.BadRequest(result.Error),
                _ => Results.BadRequest(result.Error)
            };
    }

    private static async Task<IResult> BulkRemoveDeviceTags(
        Guid groupId,
        [FromQuery] Guid tenantId,
        BulkTagRequest request,
        [FromServices] BulkRemoveDeviceTagsHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new BulkRemoveDeviceTagsCommand(tenantId, groupId, request.Tag);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.Code switch
            {
                "DEVICE_GROUP_NOT_FOUND" => Results.NotFound(result.Error),
                "DEVICE_GROUP_ACCESS_DENIED" => Results.Forbid(),
                _ => Results.BadRequest(result.Error)
            };
    }

    public record AddDeviceToGroupRequest(Guid DeviceId);
    public record UpdateDeviceGroupRequest(string? Name, string? Description, string? TagQuery);
    public record BulkTagRequest(string Tag);
}
