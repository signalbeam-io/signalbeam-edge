namespace SignalBeam.Domain.Enums;

/// <summary>
/// Notification delivery channels for alerts.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Email notification via SMTP.
    /// </summary>
    Email = 0,

    /// <summary>
    /// Slack notification via incoming webhook.
    /// </summary>
    Slack = 1,

    /// <summary>
    /// Microsoft Teams notification via incoming webhook.
    /// </summary>
    Teams = 2,

    /// <summary>
    /// PagerDuty notification via API.
    /// </summary>
    PagerDuty = 3
}
