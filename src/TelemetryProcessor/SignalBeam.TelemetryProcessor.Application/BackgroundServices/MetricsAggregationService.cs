using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service for metrics aggregation operations.
/// Note: Most aggregation is handled by TimescaleDB continuous aggregates,
/// but this service can trigger manual aggregations or cleanup tasks.
/// </summary>
public class MetricsAggregationService : BackgroundService
{
    private readonly ILogger<MetricsAggregationService> _logger;
    private readonly MetricsAggregationOptions _options;

    public MetricsAggregationService(
        ILogger<MetricsAggregationService> logger,
        IOptions<MetricsAggregationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Metrics Aggregation Service is disabled");
            return;
        }

        _logger.LogInformation(
            "Metrics Aggregation Service started. Interval: {Interval}",
            _options.AggregationInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAggregationTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Metrics Aggregation Service");
            }

            await Task.Delay(_options.AggregationInterval, stoppingToken);
        }
    }

    private async Task PerformAggregationTasksAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing metrics aggregation tasks...");

        // TimescaleDB continuous aggregates handle most of the work automatically
        // This is a placeholder for any custom aggregation or cleanup tasks

        // Example tasks that could be added:
        // 1. Clean up old raw metrics data after retention period
        // 2. Generate summary reports
        // 3. Detect anomalies in aggregated data
        // 4. Refresh materialized views if needed

        await Task.CompletedTask;

        _logger.LogDebug("Metrics aggregation tasks completed");
    }
}

/// <summary>
/// Configuration options for MetricsAggregationService.
/// </summary>
public class MetricsAggregationOptions
{
    /// <summary>
    /// Whether the aggregation service is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to run aggregation tasks.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(5);
}
