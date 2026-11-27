using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
using SignalBeam.BundleOrchestrator.Application.Services;
using Error = SignalBeam.Shared.Infrastructure.Results.Error;
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

        // Device endpoints (#489). The device's own key, verified through DeviceManager, must name the
        // route's device. Operators may read desired state with a JWT; the tenant key works only
        // outside Production. Callers that aren't the device must belong to its tenant.
        deviceGroup.MapGet("/{deviceId:guid}/desired-state", GetDeviceDesiredState)
            .WithName("GetDeviceDesiredState")
            .WithSummary("Get device desired state")
            .WithDescription("Returns the bundle the device should run, with its containers. With nothing assigned, bundleId is null and containers is empty.")
            .RequireAuthorization(AuthorizationPolicies.DeviceOrOperatorAccess);

        deviceGroup.MapPost("/{deviceId:guid}/reconciliation-status", ReportReconciliationStatus)
            .WithName("ReportReconciliationStatus")
            .WithSummary("Report reconciliation status")
            .WithDescription("Records whether the device succeeded or failed to reconcile its assigned bundle version.")
            .RequireAuthorization(AuthorizationPolicies.DeviceAccess);

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
        Guid deviceId,
        GetDeviceDesiredStateHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetDeviceDesiredStateQuery(deviceId), Caller(context), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ErrorResult(result.Error!);
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

        var result = await handler.Handle(updatedCommand, Caller(context), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ErrorResult(result.Error!);
    }

    private static DeviceCaller Caller(HttpContext context)
    {
        static Guid? ClaimGuid(HttpContext context, string type) =>
            Guid.TryParse(context.User.FindFirst(type)?.Value, out var value) ? value : null;

        return new DeviceCaller(
            ClaimGuid(context, AuthenticationConstants.TenantIdClaimType),
            ClaimGuid(context, AuthenticationConstants.DeviceIdClaimType));
    }

    private static IResult ErrorResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            new { error = error.Code, message = error.Message, type = error.Type.ToString() },
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
