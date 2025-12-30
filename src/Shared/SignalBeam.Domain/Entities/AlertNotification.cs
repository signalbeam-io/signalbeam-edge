using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a notification sent for an alert through a specific channel.
/// Tracks delivery success/failure and stores error details.
/// </summary>
public class AlertNotification : Entity<Guid>
{
    private AlertNotification()
    {
        // Required for EF Core
    }

    /// <summary>
    /// Gets the ID of the alert this notification belongs to.
    /// </summary>
    public Guid AlertId { get; private set; }

    /// <summary>
    /// Gets the notification channel used to send this notification.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Gets the recipient identifier (email address, webhook URL, etc.).
    /// </summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the notification was sent.
    /// </summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>
    /// Gets whether the notification was successfully delivered.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Gets the error message if delivery failed.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Creates a new alert notification record.
    /// </summary>
    /// <param name="alertId">Alert identifier.</param>
    /// <param name="channel">Notification channel.</param>
    /// <param name="recipient">Recipient identifier.</param>
    /// <param name="success">Whether delivery was successful.</param>
    /// <param name="error">Error message if delivery failed.</param>
    /// <returns>A new AlertNotification instance.</returns>
    public static AlertNotification Create(
        Guid alertId,
        NotificationChannel channel,
        string recipient,
        bool success,
        string? error = null)
    {
        if (alertId == Guid.Empty)
        {
            throw new ArgumentException("Alert ID cannot be empty.", nameof(alertId));
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("Recipient cannot be empty.", nameof(recipient));
        }

        if (!success && string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error message is required when notification fails.", nameof(error));
        }

        return new AlertNotification
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            Channel = channel,
            Recipient = recipient,
            SentAt = DateTimeOffset.UtcNow,
            Success = success,
            Error = error
        };
    }

    /// <summary>
    /// Gets the channel display name for logging/UI purposes.
    /// </summary>
    public string GetChannelDisplayName() => Channel switch
    {
        NotificationChannel.Email => "Email",
        NotificationChannel.Slack => "Slack",
        NotificationChannel.Teams => "Microsoft Teams",
        NotificationChannel.PagerDuty => "PagerDuty",
        _ => Channel.ToString()
    };
}
