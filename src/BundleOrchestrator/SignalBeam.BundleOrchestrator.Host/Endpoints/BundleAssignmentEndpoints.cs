using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
using ErrorType = SignalBeam.Shared.Infrastructure.Results.ErrorType;

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
            .WithTags("Bundle Assignments");

        // Operator action: assigning a bundle to a device is a control-plane write.
        deviceGroup.MapPost("/{deviceId}/bundle", AssignBundleToDevice)
            .WithName("AssignBundleToDevice")
            .WithSummary("Assign bundle to device")
            .WithDescription("Assigns a specific bundle version to a device, updating its desired state.")
            .RequireAuthorization(AuthorizationPolicies.OperatorAccess);

        // Device action: the agent polls its own desired state to reconcile — keep the existing auth
        // path (this is the reconcile/desired-state endpoint the device-endpoint carve-out preserves).
        deviceGroup.MapGet("/{deviceId}/desired-state", GetDeviceDesiredState)
            .WithName("GetDeviceDesiredState")
            .WithSummary("Get device desired state")
            .WithDescription("Retrieves the desired bundle state for a specific device.");

        // Device action: the agent reports the outcome of reconciling its desired state, which
        // updates the device's rollout status. Same device auth path as desired-state.
        deviceGroup.MapPost("/{deviceId:guid}/reconciliation-status", ReportReconciliationStatus)
            .WithName("ReportReconciliationStatus")
            .WithSummary("Report reconciliation status")
            .WithDescription("Records whether the device succeeded or failed to reconcile its assigned bundle version.");

        // Operator action: assigning a bundle to a whole group.
        var groupGroup = app.MapGroup("/api/device-groups")
            .WithTags("Bundle Assignments")
            .RequireAuthorization(AuthorizationPolicies.OperatorAccess);

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

    private static async Task<IResult> ReportReconciliationStatus(
        Guid deviceId,
        ReportReconciliationStatusCommand command,
        ReportReconciliationStatusHandler handler,
        IValidator<ReportReconciliationStatusCommand> validator,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var updatedCommand = command with { DeviceId = deviceId };
        var validation = await validator.ValidateAsync(updatedCommand, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new
            {
                error = "VALIDATION_FAILED",
                message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)),
                type = "Validation"
            });
        }

        var callerTenantId = Guid.TryParse(
            context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value, out var tenantId)
            ? tenantId
            : (Guid?)null;

        var result = await handler.Handle(updatedCommand, callerTenantId, cancellationToken);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var statusCode = result.Error!.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            new { error = result.Error.Code, message = result.Error.Message, type = result.Error.Type.ToString() },
            statusCode: statusCode);
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
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var statusCode = result.Error!.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            new { error = result.Error.Code, message = result.Error.Message, type = result.Error.Type.ToString() },
            statusCode: statusCode);
    }
}
