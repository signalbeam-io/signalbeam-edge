using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAlertNotificationRepository.
/// </summary>
public class AlertNotificationRepository : IAlertNotificationRepository
{
    private readonly TelemetryDbContext _context;

    public AlertNotificationRepository(TelemetryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AlertNotification notification, CancellationToken cancellationToken = default)
    {
        await _context.AlertNotifications.AddAsync(notification, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<AlertNotification> notifications, CancellationToken cancellationToken = default)
    {
        await _context.AlertNotifications.AddRangeAsync(notifications, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertNotification>> GetByAlertIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertNotifications
            .Where(n => n.AlertId == alertId)
            .OrderBy(n => n.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertNotification>> GetFailedNotificationsAsync(
        DateTimeOffset since,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AlertNotifications
            .Where(n => !n.Success && n.SentAt >= since)
            .OrderByDescending(n => n.SentAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<AlertNotification>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<NotificationChannel, NotificationStats>> GetNotificationStatsByChannelAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var stats = await _context.AlertNotifications
            .Where(n => n.SentAt >= since)
            .GroupBy(n => n.Channel)
            .Select(g => new
            {
                Channel = g.Key,
                TotalSent = g.Count(),
                SuccessCount = g.Count(n => n.Success),
                FailureCount = g.Count(n => !n.Success)
            })
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(
            s => s.Channel,
            s => new NotificationStats(
                s.Channel,
                s.TotalSent,
                s.SuccessCount,
                s.FailureCount,
                s.TotalSent > 0 ? (double)s.SuccessCount / s.TotalSent * 100 : 0));
    }

    public async Task<IReadOnlyList<AlertNotification>> GetRecentNotificationsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertNotifications
            .OrderByDescending(n => n.SentAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
