using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAlertRepository.
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly TelemetryDbContext _context;

    public AlertRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        await _context.Alerts.AddAsync(alert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Alert?> FindByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);
    }

    public async Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _context.Alerts.Update(alert);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Alert?> GetActiveAlertByDeviceAndTypeAsync(
        DeviceId deviceId,
        AlertType alertType,
        CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Where(a => a.DeviceId == deviceId &&
                        a.Type == alertType &&
                        a.Status == AlertStatus.Active)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts.Where(a => a.Status == AlertStatus.Active);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetActiveAlertsBySeverityAsync(
        AlertSeverity severity,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts.Where(a => a.Status == AlertStatus.Active && a.Severity == severity);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsByDeviceIdAsync(
        DeviceId deviceId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts
            .Where(a => a.DeviceId == deviceId)
            .OrderByDescending(a => a.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<Alert>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsByTimeRangeAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts.Where(a => a.CreatedAt >= startTime && a.CreatedAt <= endTime);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<AlertType, int>> GetActiveAlertCountsByTypeAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts.Where(a => a.Status == AlertStatus.Active);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .GroupBy(a => a.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);
    }

    public async Task<int> AutoResolveAlertsAsync(
        DeviceId deviceId,
        AlertType alertType,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _context.Alerts
            .Where(a => a.DeviceId == deviceId &&
                        a.Type == alertType &&
                        a.Status != AlertStatus.Resolved)
            .ToListAsync(cancellationToken);

        foreach (var alert in alerts)
        {
            alert.Resolve(resolvedAt);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return alerts.Count;
    }

    public async Task<IReadOnlyList<Alert>> GetStaleAlertsAsync(
        TimeSpan activeDuration,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(activeDuration);

        var query = _context.Alerts.Where(a => a.Status == AlertStatus.Active && a.CreatedAt <= cutoffTime);

        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return await query
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
