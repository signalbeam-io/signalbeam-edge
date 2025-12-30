using Microsoft.AspNetCore.Mvc;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
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
        var group = app.MapGroup("/api/alerts")
            .WithTags("Alerts");

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
        var query = new GetAlertsQuery
        {
            Status = status,
            Severity = severity,
            Type = type,
            DeviceId = deviceId.HasValue ? new DeviceId(deviceId.Value) : null,
            TenantId = tenantId.HasValue ? new TenantId(tenantId.Value) : null,
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
        CancellationToken cancellationToken)
    {
        var query = new GetAlertByIdQuery { AlertId = alertId };

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
        CancellationToken cancellationToken,
        [FromQuery] Guid? tenantId = null)
    {
        var query = new GetAlertStatisticsQuery
        {
            TenantId = tenantId.HasValue ? new TenantId(tenantId.Value) : null
        };

        var response = await handler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> AcknowledgeAlert(
        Guid alertId,
        AcknowledgeAlertRequest request,
        AcknowledgeAlertHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new AcknowledgeAlertCommand
        {
            AlertId = alertId,
            AcknowledgedBy = request.AcknowledgedBy
        };

        var response = await handler.HandleAsync(command, cancellationToken);

        return response.Success
            ? Results.Ok(response)
            : Results.BadRequest(new
            {
                error = "ACKNOWLEDGE_FAILED",
                message = response.ErrorMessage
            });
    }

    private static async Task<IResult> ResolveAlert(
        Guid alertId,
        ResolveAlertHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ResolveAlertCommand { AlertId = alertId };

        var response = await handler.HandleAsync(command, cancellationToken);

        return response.Success
            ? Results.Ok(response)
            : Results.BadRequest(new
            {
                error = "RESOLVE_FAILED",
                message = response.ErrorMessage
            });
    }

    /// <summary>
    /// Request body for acknowledging an alert.
    /// </summary>
    public record AcknowledgeAlertRequest(string AcknowledgedBy);
}
