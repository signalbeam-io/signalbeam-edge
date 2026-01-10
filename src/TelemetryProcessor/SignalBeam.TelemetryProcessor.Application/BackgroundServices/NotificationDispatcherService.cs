using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services.Notifications;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service that monitors for new alerts and dispatches notifications.
/// Runs periodically to check for alerts that don't have notifications yet.
/// </summary>
public class NotificationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<NotificationDispatcherService> _logger;
    private readonly NotificationDispatcherOptions _options;

    public NotificationDispatcherService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<NotificationDispatcherService> logger,
        IOptions<NotificationDispatcherOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("NotificationDispatcherService is disabled");
            return;
        }

        _logger.LogInformation(
            "NotificationDispatcherService started. Check interval: {Interval}",
            _options.CheckInterval);

        using var timer = new PeriodicTimer(_options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification dispatch cycle");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }

        _logger.LogInformation("NotificationDispatcherService stopped");
    }

    private async Task DispatchPendingNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var alertRepository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<IAlertNotificationRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IAlertNotificationService>();
        
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Starting notification dispatch cycle");

        // Get recent active alerts (last 5 minutes to catch new ones)
        var since = DateTimeOffset.UtcNow.AddMinutes(-5);
        var recentAlerts = await alertRepository.GetAlertsByTimeRangeAsync(
            since,
            DateTimeOffset.UtcNow,
            null,
            cancellationToken);

        if (!recentAlerts.Any())
        {
            _logger.LogDebug("No recent alerts found");
            return;
        }

        _logger.LogDebug("Found {Count} recent alerts to check", recentAlerts.Count);

        var notificationsSent = 0;

        foreach (var alert in recentAlerts)
        {
            try
            {
                // Check if notifications already sent for this alert
                var existingNotifications = await notificationRepository.GetByAlertIdAsync(
                    alert.Id,
                    cancellationToken);

                if (existingNotifications.Any())
                {
                    _logger.LogDebug(
                        "Alert {AlertId} already has {Count} notifications, skipping",
                        alert.Id,
                        existingNotifications.Count);
                    continue;
                }

                // Send notifications
                _logger.LogInformation(
                    "Dispatching notifications for alert {AlertId} ({Severity} - {Type})",
                    alert.Id,
                    alert.Severity,
                    alert.Type);

                var notifications = await notificationService.SendNotificationsAsync(
                    alert,
                    cancellationToken);

                notificationsSent += notifications.Count;

                _logger.LogInformation(
                    "Sent {Count} notifications for alert {AlertId}",
                    notifications.Count,
                    alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error dispatching notifications for alert {AlertId}",
                    alert.Id);
                // Continue with other alerts
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        if (notificationsSent > 0)
        {
            _logger.LogInformation(
                "Notification dispatch cycle completed. Sent {NotificationCount} notifications in {Duration}ms",
                notificationsSent,
                duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogDebug(
                "Notification dispatch cycle completed. No new notifications sent. Duration: {Duration}ms",
                duration.TotalMilliseconds);
        }
    }
}

/// <summary>
/// Configuration options for the NotificationDispatcherService.
/// </summary>
public class NotificationDispatcherOptions
{
    public const string SectionName = "NotificationDispatcher";

    /// <summary>
    /// Whether the notification dispatcher service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to check for new alerts that need notifications.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}
