using Microsoft.AspNetCore.Mvc;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Authentication.Authorization;
using SignalBeam.TelemetryProcessor.Application.Commands;
using SignalBeam.TelemetryProcessor.Application.Queries;

namespace SignalBeam.TelemetryProcessor.Host.Endpoints;

/// <summary>
/// Alert Management API endpoints.
/// Provides endpoints for querying, acknowledging, and resolving alerts.
/// </summary>
public static class AlertEndpoints
{
    /// <summary>
    /// Maps all alert-related endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        // Operator endpoints, scoped to the caller's tenant (#479). Routed by the gateway.
        var group = app.MapGroup("/api/alerts")
            .WithTags("Alerts")
            .RequireAuthorization(AuthorizationPolicies.OperatorAccess);

        group.MapGet("/", GetAlerts)
            .WithName("GetAlerts")
            .WithSummary("Get alerts with filtering")
            .WithDescription("Retrieves alerts with optional filtering by status, severity, type, device, and date range.");

        group.MapGet("/{alertId:guid}", GetAlertById)
            .WithName("GetAlertById")
            .WithSummary("Get alert by ID")
            .WithDescription("Retrieves a single alert with its notification history.");

        group.MapGet("/statistics", GetAlertStatistics)
            .WithName("GetAlertStatistics")
            .WithSummary("Get alert statistics")
            .WithDescription("Retrieves alert metrics including counts by severity, type, and stale alerts.");

        group.MapPost("/{alertId:guid}/acknowledge", AcknowledgeAlert)
            .WithName("AcknowledgeAlert")
            .WithSummary("Acknowledge an alert")
            .WithDescription("Marks an alert as acknowledged by a user.");

        group.MapPost("/{alertId:guid}/resolve", ResolveAlert)
            .WithName("ResolveAlert")
            .WithSummary("Resolve an alert")
            .WithDescription("Marks an alert as resolved, indicating the issue has been fixed.");

        return app;
    }

    private static async Task<IResult> GetAlerts(
        GetAlertsHandler handler,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] AlertStatus? status = null,
        [FromQuery] AlertSeverity? severity = null,
        [FromQuery] AlertType? type = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTimeOffset? createdAfter = null,
        [FromQuery] DateTimeOffset? createdBefore = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return TenantRequired();
        }

        var query = new GetAlertsQuery
        {
            Status = status,
            Severity = severity,
            Type = type,
            DeviceId = deviceId.HasValue ? new DeviceId(deviceId.Value) : null,
            TenantId = resolvedTenantId,
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            Limit = limit,
            Offset = offset
        };

        var response = await handler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetAlertById(
        Guid alertId,
        GetAlertByIdHandler handler,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return TenantRequired();
        }

        var query = new GetAlertByIdQuery { AlertId = alertId, TenantId = resolvedTenantId };

        var response = await handler.HandleAsync(query, cancellationToken);

        return response != null
            ? Results.Ok(response)
            : Results.NotFound(new
            {
                error = "ALERT_NOT_FOUND",
                message = $"Alert with ID {alertId} not found"
            });
    }

    private static async Task<IResult> GetAlertStatistics(
        GetAlertStatisticsHandler handler,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return TenantRequired();
        }

        var query = new GetAlertStatisticsQuery { TenantId = resolvedTenantId };

        var response = await handler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> AcknowledgeAlert(
        Guid alertId,
        AcknowledgeAlertRequest request,
        AcknowledgeAlertHandler handler,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return TenantRequired();
        }

        var command = new AcknowledgeAlertCommand
        {
            AlertId = alertId,
            AcknowledgedBy = request.AcknowledgedBy,
            TenantId = resolvedTenantId
        };

        var response = await handler.HandleAsync(command, cancellationToken);

        return response.Success
            ? Results.Ok(response)
            : CommandFailed("ACKNOWLEDGE_FAILED", response.NotFound, response.ErrorMessage);
    }

    private static async Task<IResult> ResolveAlert(
        Guid alertId,
        ResolveAlertHandler handler,
        HttpContext context,
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        if (!TryResolveTenantId(tenantId, context, out var resolvedTenantId))
        {
            return TenantRequired();
        }

        var command = new ResolveAlertCommand { AlertId = alertId, TenantId = resolvedTenantId };

        var response = await handler.HandleAsync(command, cancellationToken);

        return response.Success
            ? Results.Ok(response)
            : CommandFailed("RESOLVE_FAILED", response.NotFound, response.ErrorMessage);
    }

    // Same rule as DeviceManager: the credential's tenant claim wins; the tenantId parameter is
    // only used when the credential carries none (an operator JWT without a tenant claim; #492).
    private static bool TryResolveTenantId(Guid? tenantId, HttpContext context, out TenantId resolvedTenantId)
    {
        var claim = context.User.FindFirst(AuthenticationConstants.TenantIdClaimType)?.Value;
        if (Guid.TryParse(claim, out var claimTenant) && claimTenant != Guid.Empty)
        {
            resolvedTenantId = new TenantId(claimTenant);
            return true;
        }

        if (tenantId is { } explicitTenant && explicitTenant != Guid.Empty)
        {
            resolvedTenantId = new TenantId(explicitTenant);
            return true;
        }

        resolvedTenantId = default;
        return false;
    }

    private static IResult TenantRequired() => Results.Json(
        new { error = "TENANT_REQUIRED", message = "A tenant is required.", type = "Forbidden" },
        statusCode: StatusCodes.Status403Forbidden);

    // Alerts of another tenant look like missing ones.
    private static IResult CommandFailed(string code, bool notFound, string? message) =>
        notFound
            ? Results.NotFound(new { error = "ALERT_NOT_FOUND", message, type = "NotFound" })
            : Results.BadRequest(new { error = code, message, type = "Validation" });

    /// <summary>
    /// Request body for acknowledging an alert.
    /// </summary>
    public record AcknowledgeAlertRequest(string AcknowledgedBy);
}
