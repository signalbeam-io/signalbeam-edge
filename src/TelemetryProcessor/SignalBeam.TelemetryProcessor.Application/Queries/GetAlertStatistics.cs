using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get alert statistics and metrics.
/// </summary>
public record GetAlertStatisticsQuery
{
    public TenantId? TenantId { get; init; }
}

/// <summary>
/// Response containing alert statistics.
/// </summary>
public record GetAlertStatisticsResponse
{
    public int TotalActive { get; init; }
    public int TotalAcknowledged { get; init; }
    public int TotalResolved { get; init; }
    public AlertCountsBySeverity BySeverity { get; init; } = new();
    public AlertCountsByType ByType { get; init; } = new();
    public IReadOnlyList<StaleAlertInfo> StaleAlerts { get; init; } = Array.Empty<StaleAlertInfo>();
}

/// <summary>
/// Alert counts grouped by severity.
/// </summary>
public record AlertCountsBySeverity
{
    public int Info { get; init; }
    public int Warning { get; init; }
    public int Critical { get; init; }
}

/// <summary>
/// Alert counts grouped by type.
/// </summary>
public record AlertCountsByType
{
    public Dictionary<string, int> Counts { get; init; } = new();
}

/// <summary>
/// Information about stale (long-standing active) alerts.
/// </summary>
public record StaleAlertInfo
{
    public Guid AlertId { get; init; }
    public AlertType Type { get; init; }
    public AlertSeverity Severity { get; init; }
    public Guid? DeviceId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public TimeSpan Age { get; init; }
}

/// <summary>
/// Handler for GetAlertStatisticsQuery.
/// </summary>
public class GetAlertStatisticsHandler
{
    private readonly IAlertRepository _alertRepository;

    public GetAlertStatisticsHandler(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<GetAlertStatisticsResponse> HandleAsync(
        GetAlertStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Get all active alerts
        var activeAlerts = await _alertRepository.GetActiveAlertsAsync(
            query.TenantId,
            cancellationToken);

        // Get active alert counts by type
        var countsByType = await _alertRepository.GetActiveAlertCountsByTypeAsync(
            query.TenantId,
            cancellationToken);

        // Get stale alerts (active for more than 24 hours)
        var staleDuration = TimeSpan.FromHours(24);
        var staleAlerts = await _alertRepository.GetStaleAlertsAsync(
            staleDuration,
            query.TenantId,
            cancellationToken);

        // Calculate statistics
        var totalActive = activeAlerts.Count;
        var bySeverity = new AlertCountsBySeverity
        {
            Info = activeAlerts.Count(a => a.Severity == AlertSeverity.Info),
            Warning = activeAlerts.Count(a => a.Severity == AlertSeverity.Warning),
            Critical = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical)
        };

        var byType = new AlertCountsByType
        {
            Counts = countsByType.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value)
        };

        var staleAlertInfos = staleAlerts.Select(a => new StaleAlertInfo
        {
            AlertId = a.Id,
            Type = a.Type,
            Severity = a.Severity,
            DeviceId = a.DeviceId?.Value,
            CreatedAt = a.CreatedAt,
            Age = DateTimeOffset.UtcNow - a.CreatedAt
        }).ToList();

        // For total acknowledged/resolved, we'd need to query last N days
        // For MVP, we'll just count from recent alerts (last 7 days)
        var recentStart = DateTimeOffset.UtcNow.AddDays(-7);
        var recentAlerts = await _alertRepository.GetAlertsByTimeRangeAsync(
            recentStart,
            DateTimeOffset.UtcNow,
            query.TenantId,
            cancellationToken);

        var totalAcknowledged = recentAlerts.Count(a => a.Status == AlertStatus.Acknowledged);
        var totalResolved = recentAlerts.Count(a => a.Status == AlertStatus.Resolved);

        return new GetAlertStatisticsResponse
        {
            TotalActive = totalActive,
            TotalAcknowledged = totalAcknowledged,
            TotalResolved = totalResolved,
            BySeverity = bySeverity,
            ByType = byType,
            StaleAlerts = staleAlertInfos
        };
    }
}
