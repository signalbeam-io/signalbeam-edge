using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for device metrics time-series data.
/// </summary>
public interface IDeviceMetricsRepository
{
    /// <summary>
    /// Adds metrics to the time-series database.
    /// </summary>
    Task AddAsync(DeviceMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent metrics for a device.
    /// </summary>
    Task<DeviceMetrics?> GetLatestByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for a device within a time range.
    /// </summary>
    Task<IReadOnlyCollection<DeviceMetrics>> GetByDeviceIdAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated metrics history for a device.
    /// </summary>
    Task<(IReadOnlyCollection<DeviceMetrics> Metrics, int TotalCount)> GetMetricsHistoryAsync(
        DeviceId deviceId,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
