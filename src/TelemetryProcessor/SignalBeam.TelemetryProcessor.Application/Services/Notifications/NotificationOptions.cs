using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Services.Notifications;

/// <summary>
/// Configuration options for the alert notification system.
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Whether the notification system is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed notifications.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts (exponential backoff multiplier).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Notification channel configurations.
    /// </summary>
    public NotificationChannelConfigurations Channels { get; set; } = new();

    /// <summary>
    /// Notification routing rules (which alerts go to which channels).
    /// </summary>
    public NotificationRoutingOptions Routing { get; set; } = new();
}

/// <summary>
/// Configuration for individual notification channels.
/// </summary>
public class NotificationChannelConfigurations
{
    public EmailChannelOptions Email { get; set; } = new();
    public SlackChannelOptions Slack { get; set; } = new();
    public TeamsChannelOptions Teams { get; set; } = new();
    public PagerDutyChannelOptions PagerDuty { get; set; } = new();
}

/// <summary>
/// Email notification channel configuration.
/// </summary>
public class EmailChannelOptions
{
    public bool Enabled { get; set; } = false;
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; } = "SignalBeam Alerts";
    public List<string> DefaultRecipients { get; set; } = new();
}

/// <summary>
/// Slack notification channel configuration.
/// </summary>
public class SlackChannelOptions
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
    public string? Channel { get; set; } = "#alerts";
    public string? BotName { get; set; } = "SignalBeam";
    public string? IconEmoji { get; set; } = ":warning:";
}

/// <summary>
/// Microsoft Teams notification channel configuration.
/// </summary>
public class TeamsChannelOptions
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
}

/// <summary>
/// PagerDuty notification channel configuration.
/// </summary>
public class PagerDutyChannelOptions
{
    public bool Enabled { get; set; } = false;
    public string? IntegrationKey { get; set; }
    public string? ApiUrl { get; set; } = "https://events.pagerduty.com/v2/enqueue";
}

/// <summary>
/// Notification routing configuration.
/// Defines which alert severities go to which channels.
/// </summary>
public class NotificationRoutingOptions
{
    /// <summary>
    /// Channels to notify for Info severity alerts.
    /// </summary>
    public List<string> InfoChannels { get; set; } = new() { "Email" };

    /// <summary>
    /// Channels to notify for Warning severity alerts.
    /// </summary>
    public List<string> WarningChannels { get; set; } = new() { "Email", "Slack" };

    /// <summary>
    /// Channels to notify for Critical severity alerts.
    /// </summary>
    public List<string> CriticalChannels { get; set; } = new() { "Email", "Slack", "Teams", "PagerDuty" };

    /// <summary>
    /// Gets the channels for a specific alert severity.
    /// </summary>
    public List<string> GetChannelsForSeverity(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Info => InfoChannels,
            AlertSeverity.Warning => WarningChannels,
            AlertSeverity.Critical => CriticalChannels,
            _ => new List<string>()
        };
    }
}
