using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Services.Notifications;

/// <summary>
/// Interface for notification channel implementations.
/// Each channel (Email, Slack, Teams, PagerDuty) implements this interface.
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// The notification channel type this implementation handles.
    /// </summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Whether this notification channel is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends a notification for the specified alert.
    /// </summary>
    /// <param name="alert">The alert to send notification for.</param>
    /// <param name="recipient">The recipient address (email, webhook URL, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<NotificationResult> SendAsync(
        Alert alert,
        string recipient,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a notification send attempt.
/// </summary>
public record NotificationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset SentAt { get; init; }

    public static NotificationResult Succeeded(DateTimeOffset sentAt) =>
        new() { Success = true, SentAt = sentAt };

    public static NotificationResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage, SentAt = DateTimeOffset.UtcNow };
}
