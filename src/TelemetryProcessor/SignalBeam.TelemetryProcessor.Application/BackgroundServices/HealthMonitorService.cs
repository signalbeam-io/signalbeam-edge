using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Configuration options for Health Monitor Service.
/// </summary>
public class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";

    /// <summary>
    /// How often to calculate health scores (default: 30 seconds).
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of devices to process in one batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Background service that calculates and stores device health scores periodically.
///
/// Responsibilities:
/// - Calculate health scores for all active devices every 30 seconds
/// - Store health scores in TimescaleDB hypertable
/// - Log warnings for unhealthy devices
///
/// Health score components:
/// - Heartbeat recency (0-40 points)
/// - Reconciliation success (0-30 points)
/// - Resource utilization (0-30 points)
/// Total: 0-100 (higher is better)
/// </summary>
public class HealthMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthMonitorService> _logger;
    private readonly HealthMonitorOptions _options;

    public HealthMonitorService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthMonitorService> logger,
        IOptions<HealthMonitorOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Health Monitor Service is disabled");
            return;
        }

        _logger.LogInformation(
            "Health Monitor Service started. Check interval: {Interval}, Batch size: {BatchSize}",
            _options.CheckInterval,
            _options.BatchSize);

        using var timer = new PeriodicTimer(_options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CalculateHealthScoresAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Health Monitor Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitoring loop");
                // Wait before retrying to avoid rapid error loops
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task CalculateHealthScoresAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var heartbeatRepository = scope.ServiceProvider.GetRequiredService<IDeviceHeartbeatRepository>();
        var metricsRepository = scope.ServiceProvider.GetRequiredService<IDeviceMetricsRepository>();
        var healthScoreRepository = scope.ServiceProvider.GetRequiredService<IDeviceHealthScoreRepository>();

        try
        {
            // Get unique device IDs from recent heartbeats (last 24 hours)
            var since = DateTimeOffset.UtcNow.AddHours(-24);
            var recentDeviceIds = await heartbeatRepository.GetActiveDeviceIdsAsync(since, cancellationToken);

            _logger.LogDebug("Calculating health scores for {DeviceCount} devices with recent heartbeats", recentDeviceIds.Count);

            var healthScores = new List<SignalBeam.Domain.Entities.DeviceHealthScore>();
            var unhealthyDevices = new List<(string DeviceId, int Score)>();

            // Process devices in batches to avoid memory issues with large fleets
            foreach (var batch in recentDeviceIds.Chunk(_options.BatchSize))
            {
                foreach (var deviceId in batch)
                {
                    try
                    {
                        // Get latest heartbeat for the device
                        var latestHeartbeat = await heartbeatRepository.GetLatestByDeviceIdAsync(
                            deviceId,
                            cancellationToken);

                        if (latestHeartbeat == null)
                        {
                            _logger.LogWarning("No heartbeat found for device {DeviceId}", deviceId);
                            continue;
                        }

                        // Get latest metrics for the device
                        var latestMetrics = await metricsRepository.GetLatestByDeviceIdAsync(
                            deviceId,
                            cancellationToken);

                        // Calculate health score based on heartbeat and metrics
                        var healthScore = CalculateHealthScoreFromHeartbeatAndMetrics(
                            deviceId,
                            latestHeartbeat.Timestamp,
                            latestMetrics);

                        healthScores.Add(healthScore);

                        // Track unhealthy devices for logging
                        if (healthScore.IsUnhealthy())
                        {
                            unhealthyDevices.Add((deviceId.Value.ToString(), healthScore.TotalScore));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to calculate health score for device {DeviceId}",
                            deviceId);
                    }
                }

                // Save batch to database
                if (healthScores.Any())
                {
                    await healthScoreRepository.AddRangeAsync(healthScores, cancellationToken);
                    healthScores.Clear();
                }
            }

            // Log summary
            if (unhealthyDevices.Any())
            {
                _logger.LogWarning(
                    "Found {UnhealthyCount} unhealthy devices: {UnhealthyDevices}",
                    unhealthyDevices.Count,
                    string.Join(", ", unhealthyDevices.Select(d => $"{d.DeviceId} (score: {d.Score})")));
            }
            else
            {
                _logger.LogDebug("All devices with recent heartbeats are healthy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate health scores");
            throw;
        }
    }

    /// <summary>
    /// Calculates health score from heartbeat and metrics data.
    /// Simplified version that doesn't require Device entity from DeviceManager.
    /// </summary>
    private SignalBeam.Domain.Entities.DeviceHealthScore CalculateHealthScoreFromHeartbeatAndMetrics(
        SignalBeam.Domain.ValueObjects.DeviceId deviceId,
        DateTimeOffset lastHeartbeat,
        SignalBeam.Domain.Entities.DeviceMetrics? metrics)
    {
        var now = DateTimeOffset.UtcNow;

        // Heartbeat score (0-40)
        var secondsSinceHeartbeat = (now - lastHeartbeat).TotalSeconds;
        var heartbeatScore = secondsSinceHeartbeat switch
        {
            <= 60 => 40,   // <1 min: excellent
            <= 120 => 30,  // 1-2 min: good
            <= 180 => 20,  // 2-3 min: acceptable
            <= 300 => 10,  // 3-5 min: degraded
            _ => 0         // >5 min: critical
        };

        // Reconciliation score (0-30)
        // Without Device entity, assume healthy if heartbeat is recent
        var reconciliationScore = heartbeatScore > 20 ? 30 : 15;

        // Resource score (0-30)
        var resourceScore = 30;
        if (metrics != null)
        {
            if (metrics.CpuUsage > 95) resourceScore -= 10;
            else if (metrics.CpuUsage > 90) resourceScore -= 8;
            else if (metrics.CpuUsage > 80) resourceScore -= 5;

            if (metrics.MemoryUsage > 95) resourceScore -= 10;
            else if (metrics.MemoryUsage > 90) resourceScore -= 8;
            else if (metrics.MemoryUsage > 80) resourceScore -= 5;

            if (metrics.DiskUsage > 95) resourceScore -= 10;
            else if (metrics.DiskUsage > 90) resourceScore -= 8;
            else if (metrics.DiskUsage > 80) resourceScore -= 5;

            resourceScore = Math.Max(0, resourceScore);
        }
        else
        {
            // No metrics available, use neutral score
            resourceScore = 15;
        }

        return SignalBeam.Domain.Entities.DeviceHealthScore.Create(
            deviceId,
            heartbeatScore,
            reconciliationScore,
            resourceScore,
            now);
    }
}
