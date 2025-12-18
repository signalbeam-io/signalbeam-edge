using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for querying aggregated metrics from TimescaleDB continuous aggregates.
/// </summary>
public interface IMetricsAggregateRepository
{
    /// <summary>
    /// Gets hourly aggregated metrics for a device.
    /// </summary>
    Task<IReadOnlyCollection<HourlyMetricsAggregate>> GetHourlyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets daily aggregated metrics for a device.
    /// </summary>
    Task<IReadOnlyCollection<DailyMetricsAggregate>> GetDailyAggregatesAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated metrics for multiple devices.
    /// </summary>
    Task<IReadOnlyCollection<HourlyMetricsAggregate>> GetHourlyAggregatesForDevicesAsync(
        IEnumerable<DeviceId> deviceIds,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents hourly aggregated metrics.
/// </summary>
public record HourlyMetricsAggregate(
    DeviceId DeviceId,
    DateTimeOffset Bucket,
    double AvgCpuUsage,
    double MaxCpuUsage,
    double MinCpuUsage,
    double AvgMemoryUsage,
    double MaxMemoryUsage,
    double MinMemoryUsage,
    double AvgDiskUsage,
    double MaxDiskUsage,
    double MinDiskUsage,
    int DataPoints);

/// <summary>
/// Represents daily aggregated metrics.
/// </summary>
public record DailyMetricsAggregate(
    DeviceId DeviceId,
    DateTimeOffset Bucket,
    double AvgCpuUsage,
    double MaxCpuUsage,
    double MinCpuUsage,
    double AvgMemoryUsage,
    double MaxMemoryUsage,
    double MinMemoryUsage,
    double AvgDiskUsage,
    double MaxDiskUsage,
    double MinDiskUsage,
    int DataPoints);
