using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Infrastructure.Persistence;

namespace SignalBeam.DeviceManager.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically checks for expiring device API keys and logs warnings.
/// </summary>
public class ApiKeyExpirationCheckService : BackgroundService
{
    private readonly ILogger<ApiKeyExpirationCheckService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ApiKeyExpirationCheckOptions _options;

    public ApiKeyExpirationCheckService(
        ILogger<ApiKeyExpirationCheckService> logger,
        IServiceProvider serviceProvider,
        IOptions<ApiKeyExpirationCheckOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "API Key Expiration Check Service started. Check interval: {Interval} hours, Warning threshold: {Threshold} days",
            _options.CheckIntervalHours,
            _options.WarningThresholdDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiringKeysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for expiring API keys");
            }

            // Wait for the next check interval
            await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), stoppingToken);
        }

        _logger.LogInformation("API Key Expiration Check Service stopped");
    }

    private async Task CheckExpiringKeysAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();

        var now = DateTimeOffset.UtcNow;
        var warningThreshold = now.AddDays(_options.WarningThresholdDays);

        // Find API keys that are expiring soon and not revoked
        var expiringKeys = await context.DeviceApiKeys
            .Where(k => k.RevokedAt == null) // Not revoked
            .Where(k => k.ExpiresAt != null && k.ExpiresAt <= warningThreshold) // Expiring within threshold
            .ToListAsync(cancellationToken);

        if (!expiringKeys.Any())
        {
            _logger.LogDebug("No API keys expiring within {Threshold} days", _options.WarningThresholdDays);
            return;
        }

        _logger.LogWarning(
            "Found {Count} API keys expiring within {Threshold} days",
            expiringKeys.Count,
            _options.WarningThresholdDays);

        foreach (var key in expiringKeys)
        {
            var daysUntilExpiration = (key.ExpiresAt!.Value - now).TotalDays;

            if (daysUntilExpiration < 0)
            {
                _logger.LogWarning(
                    "API key {KeyPrefix} for device {DeviceId} has EXPIRED on {ExpirationDate}",
                    key.KeyPrefix,
                    key.DeviceId.Value,
                    key.ExpiresAt.Value.ToString("yyyy-MM-dd"));
            }
            else
            {
                _logger.LogWarning(
                    "API key {KeyPrefix} for device {DeviceId} expires in {DaysRemaining:F1} days on {ExpirationDate}",
                    key.KeyPrefix,
                    key.DeviceId.Value,
                    daysUntilExpiration,
                    key.ExpiresAt.Value.ToString("yyyy-MM-dd"));
            }

            // TODO: In the future, send notifications to administrators
            // - Email notifications
            // - Slack/Teams notifications
            // - Dashboard alerts
        }
    }
}

/// <summary>
/// Configuration options for API key expiration check service.
/// </summary>
public class ApiKeyExpirationCheckOptions
{
    public const string SectionName = "ApiKeyExpirationCheck";

    /// <summary>
    /// How often to check for expiring keys (in hours). Default: 24 hours (daily).
    /// </summary>
    public double CheckIntervalHours { get; set; } = 24.0;

    /// <summary>
    /// Warn about keys expiring within this many days. Default: 7 days.
    /// </summary>
    public int WarningThresholdDays { get; set; } = 7;

    /// <summary>
    /// Enable or disable the background service. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
