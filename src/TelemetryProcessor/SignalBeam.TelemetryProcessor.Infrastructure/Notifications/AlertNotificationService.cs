using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Notifications;

/// <summary>
/// Service for sending alert notifications through configured channels.
/// </summary>
public class AlertNotificationService : IAlertNotificationService
{
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly IAlertNotificationRepository _notificationRepository;
    private readonly ILogger<AlertNotificationService> _logger;
    private readonly NotificationOptions _options;

    public AlertNotificationService(
        IEnumerable<INotificationChannel> channels,
        IAlertNotificationRepository notificationRepository,
        ILogger<AlertNotificationService> logger,
        IOptions<NotificationOptions> options)
    {
        _channels = channels;
        _notificationRepository = notificationRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AlertNotification>> SendNotificationsAsync(
        Alert alert,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Notifications globally disabled, skipping alert {AlertId}", alert.Id);
            return Array.Empty<AlertNotification>();
        }

        _logger.LogInformation(
            "Sending notifications for alert {AlertId} ({Severity} - {Type})",
            alert.Id,
            alert.Severity,
            alert.Type);

        // Get channels to notify based on severity routing
        var channelNames = _options.Routing.GetChannelsForSeverity(alert.Severity);

        if (!channelNames.Any())
        {
            _logger.LogDebug(
                "No channels configured for severity {Severity}, skipping notifications",
                alert.Severity);
            return Array.Empty<AlertNotification>();
        }

        var notifications = new List<AlertNotification>();

        foreach (var channelName in channelNames)
        {
            // Parse channel enum
            if (!Enum.TryParse<NotificationChannel>(channelName, ignoreCase: true, out var channelEnum))
            {
                _logger.LogWarning("Unknown notification channel: {ChannelName}", channelName);
                continue;
            }

            // Find channel implementation
            var channel = _channels.FirstOrDefault(c => c.Channel == channelEnum);

            if (channel == null)
            {
                _logger.LogWarning("No implementation found for channel {ChannelName}", channelName);
                continue;
            }

            if (!channel.IsEnabled)
            {
                _logger.LogDebug("Channel {ChannelName} is disabled, skipping", channelName);
                continue;
            }

            // Get recipient for this channel
            var recipient = GetRecipientForChannel(channelEnum);

            if (string.IsNullOrEmpty(recipient))
            {
                _logger.LogWarning("No recipient configured for channel {ChannelName}", channelName);
                continue;
            }

            try
            {
                // Send notification
                var result = await channel.SendAsync(alert, recipient, cancellationToken);

                // Create notification record
                var notification = AlertNotification.Create(
                    alert.Id,
                    channelEnum,
                    recipient,
                    result.Success,
                    result.ErrorMessage);

                await _notificationRepository.AddAsync(notification, cancellationToken);
                notifications.Add(notification);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Notification sent successfully via {Channel} to {Recipient} for alert {AlertId}",
                        channelEnum,
                        recipient,
                        alert.Id);
                }
                else
                {
                    _logger.LogError(
                        "Failed to send notification via {Channel} to {Recipient} for alert {AlertId}: {Error}",
                        channelEnum,
                        recipient,
                        alert.Id,
                        result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception while sending notification via {Channel} for alert {AlertId}",
                    channelEnum,
                    alert.Id);

                // Create failed notification record
                var notification = AlertNotification.Create(
                    alert.Id,
                    channelEnum,
                    recipient,
                    false,
                    ex.Message);

                await _notificationRepository.AddAsync(notification, cancellationToken);
                notifications.Add(notification);
            }
        }

        _logger.LogInformation(
            "Sent {SuccessCount}/{TotalCount} notifications for alert {AlertId}",
            notifications.Count(n => n.Success),
            notifications.Count,
            alert.Id);

        return notifications;
    }

    public Task<AlertNotification> RetryNotificationAsync(
        AlertNotification notification,
        CancellationToken cancellationToken = default)
    {
        // MVP limitation: Retry not implemented due to missing RetryCount tracking
        // Future enhancement: Add retry_count column to alert_notifications table
        // and implement full retry logic with exponential backoff

        _logger.LogWarning(
            "Notification retry not implemented in MVP. Notification {NotificationId} for channel {Channel}",
            notification.Id,
            notification.Channel);

        return Task.FromResult(notification);
    }

    private string? GetRecipientForChannel(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => _options.Channels.Email.DefaultRecipients.FirstOrDefault(),
            NotificationChannel.Slack => _options.Channels.Slack.WebhookUrl,
            NotificationChannel.Teams => _options.Channels.Teams.WebhookUrl,
            NotificationChannel.PagerDuty => _options.Channels.PagerDuty.IntegrationKey,
            _ => null
        };
    }
}
