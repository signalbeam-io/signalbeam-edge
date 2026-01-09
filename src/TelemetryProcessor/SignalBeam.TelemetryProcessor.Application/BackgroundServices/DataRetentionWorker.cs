using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.TelemetryProcessor.Application.Services;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service that enforces data retention policies for all tenants.
/// Runs on a configurable schedule (default: daily at 2 AM UTC).
/// </summary>
public class DataRetentionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRetentionWorker> _logger;
    private readonly DataRetentionOptions _options;

    public DataRetentionWorker(
        IServiceProvider serviceProvider,
        ILogger<DataRetentionWorker> logger,
        IOptions<DataRetentionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Data Retention Worker is disabled");
            return;
        }

        _logger.LogInformation(
            "Data Retention Worker started. Schedule: Daily at {ScheduleTime} UTC",
            _options.ScheduledTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextRunTime = CalculateNextRunTime();
                var delay = nextRunTime - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "Next data retention enforcement scheduled for {NextRunTime} UTC (in {DelayHours:F1} hours)",
                        nextRunTime,
                        delay.TotalHours);

                    await Task.Delay(delay, stoppingToken);
                }

                // Run data retention enforcement
                await EnforceDataRetentionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Data Retention Worker is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Data Retention Worker");

                // Wait a short period before retrying in case of error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Data Retention Worker stopped");
    }

    private async Task EnforceDataRetentionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data retention enforcement");

        try
        {
            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var dataRetentionService = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

            var result = await dataRetentionService.EnforceDataRetentionAsync(cancellationToken);

            if (result.IsSuccess)
            {
                var summary = result.Value!;
                _logger.LogInformation(
                    "Data retention enforcement completed successfully. " +
                    "Processed {TenantCount} tenants, deleted {MetricsCount} metrics " +
                    "and {HeartbeatsCount} heartbeats in {ElapsedSeconds:F2}s",
                    summary.TenantsProcessed,
                    summary.MetricsDeleted,
                    summary.HeartbeatsDeleted,
                    summary.ElapsedTime.TotalSeconds);
            }
            else
            {
                _logger.LogError(
                    "Data retention enforcement failed: {ErrorCode} - {ErrorMessage}",
                    result.Error!.Code,
                    result.Error.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during data retention enforcement");
        }
    }

    private DateTimeOffset CalculateNextRunTime()
    {
        var now = DateTimeOffset.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        // Calculate today's scheduled time
        var todayScheduled = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            scheduledTime.Hours,
            scheduledTime.Minutes,
            scheduledTime.Seconds,
            TimeSpan.Zero);

        // If scheduled time already passed today, schedule for tomorrow
        if (now >= todayScheduled)
        {
            return todayScheduled.AddDays(1);
        }

        return todayScheduled;
    }
}

/// <summary>
/// Configuration options for DataRetentionWorker.
/// </summary>
public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    /// <summary>
    /// Whether the data retention worker is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The time of day (UTC) when data retention should run.
    /// Default: 2:00 AM UTC.
    /// </summary>
    public TimeSpan ScheduledTime { get; set; } = new TimeSpan(2, 0, 0);

    /// <summary>
    /// Minimum interval between retention runs (fallback if scheduling fails).
    /// Default: 24 hours.
    /// </summary>
    public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromHours(24);
}
