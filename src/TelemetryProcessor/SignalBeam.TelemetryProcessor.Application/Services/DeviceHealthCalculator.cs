using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Services;

/// <summary>
/// Calculates device health scores based on heartbeat recency, reconciliation success, and resource utilization.
///
/// Scoring breakdown:
/// - Heartbeat (0-40 points): How recently the device sent a heartbeat
/// - Reconciliation (0-30 points): Success rate of reconciliation attempts
/// - Resources (0-30 points): CPU, memory, and disk utilization
///
/// Total score: 0-100 (higher is better)
/// - 70-100: Healthy
/// - 40-69: Degraded
/// - 0-39: Critical
/// </summary>
public class DeviceHealthCalculator : IDeviceHealthCalculator
{
    private readonly ILogger<DeviceHealthCalculator> _logger;

    public DeviceHealthCalculator(ILogger<DeviceHealthCalculator> logger)
    {
        _logger = logger;
    }

    public DeviceHealthScore Calculate(Device device, DeviceMetrics? latestMetrics)
    {
        var now = DateTimeOffset.UtcNow;

        // Calculate component scores
        var heartbeatScore = CalculateHeartbeatScore(device.LastSeenAt, now);
        var reconciliationScore = CalculateReconciliationScore(device, latestMetrics);
        var resourceScore = CalculateResourceScore(latestMetrics);

        var healthScore = DeviceHealthScore.Create(
            device.Id,
            heartbeatScore,
            reconciliationScore,
            resourceScore,
            now);

        _logger.LogDebug(
            "Calculated health score for device {DeviceId}: Total={TotalScore}, Heartbeat={HeartbeatScore}, Reconciliation={ReconciliationScore}, Resource={ResourceScore}",
            device.Id,
            healthScore.TotalScore,
            heartbeatScore,
            reconciliationScore,
            resourceScore);

        return healthScore;
    }

    public bool IsDeviceUnhealthy(Device device, DeviceMetrics? latestMetrics, int threshold = 50)
    {
        var healthScore = Calculate(device, latestMetrics);
        return healthScore.IsUnhealthy(threshold);
    }

    /// <summary>
    /// Calculates heartbeat score (0-40 points) based on how recently the device sent a heartbeat.
    /// </summary>
    private int CalculateHeartbeatScore(DateTimeOffset? lastSeenAt, DateTimeOffset now)
    {
        if (!lastSeenAt.HasValue)
        {
            _logger.LogWarning("Device has never sent a heartbeat");
            return 0; // Never seen = critical
        }

        var secondsSinceHeartbeat = (now - lastSeenAt.Value).TotalSeconds;

        var score = secondsSinceHeartbeat switch
        {
            <= 60 => 40,   // <1 min: excellent (recent heartbeat)
            <= 120 => 30,  // 1-2 min: good
            <= 180 => 20,  // 2-3 min: acceptable
            <= 300 => 10,  // 3-5 min: degraded
            _ => 0         // >5 min: critical (offline)
        };

        if (score < 20)
        {
            _logger.LogWarning(
                "Device heartbeat is stale: {SecondsSinceHeartbeat}s ago (score: {Score}/40)",
                secondsSinceHeartbeat,
                score);
        }

        return score;
    }

    /// <summary>
    /// Calculates reconciliation score (0-30 points) based on device status and reconciliation success.
    /// </summary>
    private int CalculateReconciliationScore(Device device, DeviceMetrics? latestMetrics)
    {
        // If device is offline, reconciliation score is 0
        if (device.Status == DeviceStatus.Offline)
        {
            return 0;
        }

        // If device is in error state, reconciliation is failing
        if (device.Status == DeviceStatus.Error)
        {
            _logger.LogWarning("Device {DeviceId} is in error state", device.Id);
            return 5; // Minimal score for error state
        }

        // If device is updating, it's actively reconciling
        if (device.Status == DeviceStatus.Updating)
        {
            return 20; // Partial score during updates
        }

        // Check if device has metrics with reconciliation data
        if (latestMetrics?.AdditionalMetrics != null)
        {
            // TODO: Parse reconciliation success rate from AdditionalMetrics JSON
            // For now, assume healthy if device is online
            return device.Status == DeviceStatus.Online ? 30 : 15;
        }

        // Default: If device is online and no reconciliation issues reported, assume healthy
        return device.Status == DeviceStatus.Online ? 30 : 15;
    }

    /// <summary>
    /// Calculates resource utilization score (0-30 points) based on CPU, memory, and disk usage.
    /// Lower utilization = higher score.
    /// </summary>
    private int CalculateResourceScore(DeviceMetrics? metrics)
    {
        if (metrics == null)
        {
            _logger.LogDebug("No metrics available, using neutral resource score");
            return 15; // Neutral score when no metrics available
        }

        var score = 30;

        // CPU utilization penalty
        if (metrics.CpuUsage > 95)
        {
            score -= 10;
            _logger.LogWarning("Critical CPU usage: {CpuUsage}%", metrics.CpuUsage);
        }
        else if (metrics.CpuUsage > 90)
        {
            score -= 8;
        }
        else if (metrics.CpuUsage > 80)
        {
            score -= 5;
        }
        else if (metrics.CpuUsage > 70)
        {
            score -= 3;
        }

        // Memory utilization penalty
        if (metrics.MemoryUsage > 95)
        {
            score -= 10;
            _logger.LogWarning("Critical memory usage: {MemoryUsage}%", metrics.MemoryUsage);
        }
        else if (metrics.MemoryUsage > 90)
        {
            score -= 8;
        }
        else if (metrics.MemoryUsage > 80)
        {
            score -= 5;
        }
        else if (metrics.MemoryUsage > 70)
        {
            score -= 3;
        }

        // Disk utilization penalty
        if (metrics.DiskUsage > 95)
        {
            score -= 10;
            _logger.LogWarning("Critical disk usage: {DiskUsage}%", metrics.DiskUsage);
        }
        else if (metrics.DiskUsage > 90)
        {
            score -= 8;
        }
        else if (metrics.DiskUsage > 80)
        {
            score -= 5;
        }
        else if (metrics.DiskUsage > 70)
        {
            score -= 3;
        }

        // Ensure score doesn't go below 0
        score = Math.Max(0, score);

        if (score < 15)
        {
            _logger.LogWarning(
                "Low resource score: {Score}/30 (CPU: {Cpu}%, Memory: {Memory}%, Disk: {Disk}%)",
                score,
                metrics.CpuUsage,
                metrics.MemoryUsage,
                metrics.DiskUsage);
        }

        return score;
    }
}
