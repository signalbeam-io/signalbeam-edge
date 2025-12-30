using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for managing device health scores.
/// </summary>
public interface IDeviceHealthScoreRepository
{
    /// <summary>
    /// Adds a new health score record.
    /// </summary>
    Task AddAsync(DeviceHealthScore healthScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple health score records in bulk.
    /// </summary>
    Task AddRangeAsync(IEnumerable<DeviceHealthScore> healthScores, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest health score for a specific device.
    /// </summary>
    Task<DeviceHealthScore?> GetLatestByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health scores for a device within a time range.
    /// </summary>
    Task<IReadOnlyList<DeviceHealthScore>> GetByDeviceIdAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets device IDs with health scores below the threshold within the specified time range.
    /// </summary>
    Task<IReadOnlyList<DeviceId>> GetUnhealthyDevicesAsync(
        int healthThreshold,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets average health score for a device over a time period.
    /// </summary>
    Task<double?> GetAverageHealthScoreAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health score distribution (count of devices in each health category).
    /// </summary>
    Task<HealthScoreDistribution> GetHealthScoreDistributionAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes health scores older than the specified date (for manual cleanup).
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Health score distribution across the fleet.
/// </summary>
public record HealthScoreDistribution(
    int HealthyCount,      // 70-100
    int DegradedCount,     // 40-69
    int CriticalCount,     // 0-39
    int TotalCount,
    double AverageScore);
