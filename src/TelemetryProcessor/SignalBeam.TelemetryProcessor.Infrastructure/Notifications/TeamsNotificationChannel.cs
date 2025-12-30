using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Notifications;

/// <summary>
/// Microsoft Teams notification channel using incoming webhooks.
/// </summary>
public class TeamsNotificationChannel : INotificationChannel
{
    private readonly ILogger<TeamsNotificationChannel> _logger;
    private readonly NotificationOptions _options;
    private readonly HttpClient _httpClient;

    public NotificationChannel Channel => NotificationChannel.Teams;
    public bool IsEnabled => _options.Channels.Teams.Enabled;

    public TeamsNotificationChannel(
        ILogger<TeamsNotificationChannel> logger,
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
            _logger.LogWarning("Teams notifications are disabled");
            return NotificationResult.Failed("Teams notifications are disabled");
        }

        var config = _options.Channels.Teams;

        // Use provided webhook URL or default from config
        var webhookUrl = string.IsNullOrEmpty(recipient)
            ? config.WebhookUrl
            : recipient;

        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogError("Teams webhook URL not configured");
            return NotificationResult.Failed("Teams webhook URL not configured");
        }

        try
        {
            var payload = BuildTeamsPayload(alert);
            var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Teams notification sent successfully for alert {AlertId}",
                    alert.Id);

                return NotificationResult.Succeeded(DateTimeOffset.UtcNow);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to send Teams notification for alert {AlertId}: {StatusCode} - {Error}",
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
                "Exception while sending Teams notification for alert {AlertId}",
                alert.Id);

            return NotificationResult.Failed(ex.Message);
        }
    }

    private static object BuildTeamsPayload(Alert alert)
    {
        var themeColor = alert.Severity switch
        {
            AlertSeverity.Info => "0066CC",
            AlertSeverity.Warning => "FF9800",
            AlertSeverity.Critical => "D32F2F",
            _ => "757575"
        };

        var emoji = alert.Severity switch
        {
            AlertSeverity.Info => "ℹ️",
            AlertSeverity.Warning => "⚠️",
            AlertSeverity.Critical => "🚨",
            _ => "📢"
        };

        // Adaptive Card format for Teams
        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "Container",
                                style = "emphasis",
                                items = new[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"{emoji} {alert.Severity} Alert",
                                        weight = "Bolder",
                                        size = "Large",
                                        color = alert.Severity == AlertSeverity.Critical ? "Attention" : "Warning"
                                    }
                                }
                            },
                            new
                            {
                                type = "TextBlock",
                                text = alert.Title,
                                size = "Medium",
                                weight = "Bolder",
                                wrap = true
                            },
                            new
                            {
                                type = "TextBlock",
                                text = alert.Description,
                                wrap = true,
                                spacing = "Medium"
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new[]
                                {
                                    new { title = "Alert Type", value = alert.Type.ToString() },
                                    new { title = "Severity", value = alert.Severity.ToString() },
                                    new { title = "Device ID", value = (alert.DeviceId?.Value.ToString() ?? "N/A") },
                                    new { title = "Created At", value = $"{alert.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC" },
                                    new { title = "Alert ID", value = alert.Id.ToString() }
                                }
                            }
                        },
                        msteams = new
                        {
                            width = "Full"
                        }
                    }
                }
            }
        };
    }
}
