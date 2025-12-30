using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Services.Notifications;

/// <summary>
/// Service for sending alert notifications through configured channels.
/// </summary>
public interface IAlertNotificationService
{
    /// <summary>
    /// Sends notifications for an alert through all configured channels.
    /// Creates AlertNotification records for tracking.
    /// </summary>
    /// <param name="alert">The alert to send notifications for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of created notification records.</returns>
    Task<IReadOnlyList<AlertNotification>> SendNotificationsAsync(
        Alert alert,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries sending a failed notification.
    /// </summary>
    /// <param name="notification">The failed notification to retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated notification record.</returns>
    Task<AlertNotification> RetryNotificationAsync(
        AlertNotification notification,
        CancellationToken cancellationToken = default);
}
