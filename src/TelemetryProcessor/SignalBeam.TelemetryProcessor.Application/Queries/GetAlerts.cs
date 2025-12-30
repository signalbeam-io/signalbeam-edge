using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get alerts with optional filtering.
/// </summary>
public record GetAlertsQuery
{
    /// <summary>
    /// Filter by alert status (Active, Acknowledged, Resolved).
    /// </summary>
    public AlertStatus? Status { get; init; }

    /// <summary>
    /// Filter by alert severity (Info, Warning, Critical).
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Filter by alert type.
    /// </summary>
    public AlertType? Type { get; init; }

    /// <summary>
    /// Filter by device ID.
    /// </summary>
    public DeviceId? DeviceId { get; init; }

    /// <summary>
    /// Filter by tenant ID.
    /// </summary>
    public TenantId? TenantId { get; init; }

    /// <summary>
    /// Filter by creation date range - start.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; init; }

    /// <summary>
    /// Filter by creation date range - end.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// Number of results to skip (for pagination).
    /// </summary>
    public int Offset { get; init; } = 0;
}

/// <summary>
/// Response containing list of alerts with pagination info.
/// </summary>
public record GetAlertsResponse
{
    public IReadOnlyList<AlertDto> Alerts { get; init; } = Array.Empty<AlertDto>();
    public int TotalCount { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}

/// <summary>
/// DTO for alert data.
/// </summary>
public record AlertDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public AlertSeverity Severity { get; init; }
    public AlertType Type { get; init; }
    public AlertStatus Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid? DeviceId { get; init; }
    public Guid? RolloutId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public TimeSpan Age => DateTimeOffset.UtcNow - CreatedAt;
    public TimeSpan? TimeToAcknowledge => AcknowledgedAt.HasValue ? AcknowledgedAt.Value - CreatedAt : null;
    public TimeSpan? TimeToResolve => ResolvedAt.HasValue ? ResolvedAt.Value - CreatedAt : null;

    public static AlertDto FromEntity(Alert alert)
    {
        return new AlertDto
        {
            Id = alert.Id,
            TenantId = alert.TenantId.Value,
            Severity = alert.Severity,
            Type = alert.Type,
            Status = alert.Status,
            Title = alert.Title,
            Description = alert.Description,
            DeviceId = alert.DeviceId?.Value,
            RolloutId = alert.RolloutId,
            CreatedAt = alert.CreatedAt,
            AcknowledgedBy = alert.AcknowledgedBy,
            AcknowledgedAt = alert.AcknowledgedAt,
            ResolvedAt = alert.ResolvedAt
        };
    }
}

/// <summary>
/// Handler for GetAlertsQuery.
/// </summary>
public class GetAlertsHandler
{
    private readonly IAlertRepository _alertRepository;

    public GetAlertsHandler(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<GetAlertsResponse> HandleAsync(
        GetAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Build filters based on query
        IReadOnlyList<Alert> alerts;

        if (query.Status.HasValue)
        {
            if (query.Status == AlertStatus.Active)
            {
                if (query.Severity.HasValue)
                {
                    alerts = await _alertRepository.GetActiveAlertsBySeverityAsync(
                        query.Severity.Value,
                        query.TenantId,
                        cancellationToken);
                }
                else
                {
                    alerts = await _alertRepository.GetActiveAlertsAsync(
                        query.TenantId,
                        cancellationToken);
                }
            }
            else if (query.CreatedAfter.HasValue && query.CreatedBefore.HasValue)
            {
                alerts = await _alertRepository.GetAlertsByTimeRangeAsync(
                    query.CreatedAfter.Value,
                    query.CreatedBefore.Value,
                    query.TenantId,
                    cancellationToken);
            }
            else
            {
                // Fallback: get recent alerts (last 7 days)
                var startTime = DateTimeOffset.UtcNow.AddDays(-7);
                alerts = await _alertRepository.GetAlertsByTimeRangeAsync(
                    startTime,
                    DateTimeOffset.UtcNow,
                    query.TenantId,
                    cancellationToken);
            }
        }
        else if (query.DeviceId.HasValue)
        {
            alerts = await _alertRepository.GetAlertsByDeviceIdAsync(
                query.DeviceId.Value,
                limit: null,
                cancellationToken);
        }
        else if (query.CreatedAfter.HasValue && query.CreatedBefore.HasValue)
        {
            alerts = await _alertRepository.GetAlertsByTimeRangeAsync(
                query.CreatedAfter.Value,
                query.CreatedBefore.Value,
                query.TenantId,
                cancellationToken);
        }
        else
        {
            // Default: get active alerts
            alerts = await _alertRepository.GetActiveAlertsAsync(
                query.TenantId,
                cancellationToken);
        }

        // Apply additional filters in memory (not ideal, but works for MVP)
        var filteredAlerts = alerts.AsEnumerable();

        if (query.Status.HasValue)
        {
            filteredAlerts = filteredAlerts.Where(a => a.Status == query.Status.Value);
        }

        if (query.Severity.HasValue)
        {
            filteredAlerts = filteredAlerts.Where(a => a.Severity == query.Severity.Value);
        }

        if (query.Type.HasValue)
        {
            filteredAlerts = filteredAlerts.Where(a => a.Type == query.Type.Value);
        }

        var totalCount = filteredAlerts.Count();

        // Apply pagination
        var paginatedAlerts = filteredAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(AlertDto.FromEntity)
            .ToList();

        return new GetAlertsResponse
        {
            Alerts = paginatedAlerts,
            TotalCount = totalCount,
            Offset = query.Offset,
            Limit = query.Limit
        };
    }
}
