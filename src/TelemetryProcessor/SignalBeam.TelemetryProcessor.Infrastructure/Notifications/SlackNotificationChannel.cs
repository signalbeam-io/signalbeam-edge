using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Notifications;

/// <summary>
/// Slack notification channel using incoming webhooks.
/// </summary>
public class SlackNotificationChannel : INotificationChannel
{
    private readonly ILogger<SlackNotificationChannel> _logger;
    private readonly NotificationOptions _options;
    private readonly HttpClient _httpClient;

    public NotificationChannel Channel => NotificationChannel.Slack;
    public bool IsEnabled => _options.Channels.Slack.Enabled;

    public SlackNotificationChannel(
        ILogger<SlackNotificationChannel> logger,
        IOptions<NotificationOptions> options,
        HttpClient httpClient)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<NotificationResult> SendAsync(
        Alert alert,
        string recipient,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Slack notifications are disabled");
            return NotificationResult.Failed("Slack notifications are disabled");
        }

        var config = _options.Channels.Slack;

        // Use provided webhook URL or default from config
        var webhookUrl = string.IsNullOrEmpty(recipient) || recipient.StartsWith('#')
            ? config.WebhookUrl
            : recipient;

        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogError("Slack webhook URL not configured");
            return NotificationResult.Failed("Slack webhook URL not configured");
        }

        try
        {
            var payload = BuildSlackPayload(alert, config);
            var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Slack notification sent successfully for alert {AlertId}",
                    alert.Id);

                return NotificationResult.Succeeded(DateTimeOffset.UtcNow);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to send Slack notification for alert {AlertId}: {StatusCode} - {Error}",
                    alert.Id,
                    response.StatusCode,
                    errorContent);

                return NotificationResult.Failed($"HTTP {response.StatusCode}: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending Slack notification for alert {AlertId}",
                alert.Id);

            return NotificationResult.Failed(ex.Message);
        }
    }

    private static object BuildSlackPayload(Alert alert, SlackChannelOptions config)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Info => "#0066CC",
            AlertSeverity.Warning => "#FF9800",
            AlertSeverity.Critical => "#D32F2F",
            _ => "#757575"
        };

        var emoji = alert.Severity switch
        {
            AlertSeverity.Info => ":information_source:",
            AlertSeverity.Warning => ":warning:",
            AlertSeverity.Critical => ":rotating_light:",
            _ => ":bell:"
        };

        return new
        {
            username = config.BotName ?? "SignalBeam",
            icon_emoji = config.IconEmoji ?? ":warning:",
            channel = config.Channel ?? "#alerts",
            attachments = new[]
            {
                new
                {
                    color,
                    title = $"{emoji} {alert.Severity} Alert: {alert.Title}",
                    text = alert.Description,
                    fields = new[]
                    {
                        new { title = "Alert Type", value = alert.Type.ToString(), @short = true },
                        new { title = "Severity", value = alert.Severity.ToString(), @short = true },
                        new { title = "Device ID", value = (alert.DeviceId?.Value.ToString() ?? "N/A"), @short = true },
                        new { title = "Created At", value = $"<!date^{alert.CreatedAt.ToUnixTimeSeconds()}^{{date_short_pretty}} {{time}}|{alert.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC>", @short = true }
                    },
                    footer = "SignalBeam Alerts",
                    footer_icon = "https://signalbeam.io/icon.png",
                    ts = alert.CreatedAt.ToUnixTimeSeconds()
                }
            }
        };
    }
}
