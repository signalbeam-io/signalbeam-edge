using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service that retries failed notifications.
/// Runs periodically to find failed notifications and retry them.
/// </summary>
public class NotificationRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<NotificationRetryService> _logger;
    private readonly NotificationRetryOptions _options;

    public NotificationRetryService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<NotificationRetryService> logger,
        IOptions<NotificationRetryOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("NotificationRetryService is disabled");
            return;
        }

        _logger.LogInformation(
            "NotificationRetryService started. Check interval: {Interval}, Max retries: {MaxRetries}",
            _options.CheckInterval,
            _options.MaxRetryAttempts);

        using var timer = new PeriodicTimer(_options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryFailedNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification retry cycle");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }

        _logger.LogInformation("NotificationRetryService stopped");
    }

    private async Task RetryFailedNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<IAlertNotificationRepository>();
        
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Starting notification retry cycle");

        // Get failed notifications from last hour
        var since = DateTimeOffset.UtcNow.AddHours(-1);
        var failedNotifications = await notificationRepository.GetFailedNotificationsAsync(
            since,
            limit: 100,
            cancellationToken);

        // Note: AlertNotification entity doesn't currently track retry count
        // For MVP, we simply don't retry - just log failed notifications
        // Future enhancement: Add RetryCount column to alert_notifications table

        if (!failedNotifications.Any())
        {
            _logger.LogDebug("No failed notifications eligible for retry");
            return;
        }

        _logger.LogWarning(
            "Found {Count} failed notifications (retry not implemented in MVP)",
            failedNotifications.Count);

        // For MVP, just log failed notifications for manual investigation
        // Future enhancement: Implement retry with RetryCount tracking
        foreach (var notification in failedNotifications)
        {
            _logger.LogWarning(
                "Failed notification {NotificationId} for alert - Channel: {Channel}, Recipient: {Recipient}, Error: {Error}",
                notification.Id,
                notification.Channel,
                notification.Recipient,
                notification.Error);
        }

        var retriedCount = 0; // No retries in MVP

        var duration = DateTimeOffset.UtcNow - startTime;

        if (retriedCount > 0)
        {
            _logger.LogInformation(
                "Notification retry cycle completed. Retried {RetryCount} notifications in {Duration}ms",
                retriedCount,
                duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogDebug(
                "Notification retry cycle completed. No notifications retried. Duration: {Duration}ms",
                duration.TotalMilliseconds);
        }
    }
}

/// <summary>
/// Configuration options for the NotificationRetryService.
/// </summary>
public class NotificationRetryOptions
{
    public const string SectionName = "NotificationRetry";

    /// <summary>
    /// Whether the notification retry service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to check for failed notifications to retry.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of retry attempts.
    /// Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromMinutes(5);
}
