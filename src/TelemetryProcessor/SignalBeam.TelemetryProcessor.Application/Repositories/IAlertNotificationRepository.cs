using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for managing alert notifications.
/// </summary>
public interface IAlertNotificationRepository
{
    /// <summary>
    /// Adds a new notification record.
    /// </summary>
    Task AddAsync(AlertNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple notification records in bulk.
    /// </summary>
    Task AddRangeAsync(IEnumerable<AlertNotification> notifications, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notifications for a specific alert.
    /// </summary>
    Task<IReadOnlyList<AlertNotification>> GetByAlertIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed notifications (for retry logic).
    /// </summary>
    Task<IReadOnlyList<AlertNotification>> GetFailedNotificationsAsync(
        DateTimeOffset since,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notification success rate by channel.
    /// </summary>
    Task<Dictionary<NotificationChannel, NotificationStats>> GetNotificationStatsByChannelAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent notifications for monitoring.
    /// </summary>
    Task<IReadOnlyList<AlertNotification>> GetRecentNotificationsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for a notification channel.
/// </summary>
public record NotificationStats(
    NotificationChannel Channel,
    int TotalSent,
    int SuccessCount,
    int FailureCount,
    double SuccessRate);
