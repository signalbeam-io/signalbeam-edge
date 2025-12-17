using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Query repository for DeviceMetrics (read-only operations).
/// </summary>
public interface IDeviceMetricsQueryRepository
{
    /// <summary>
    /// Gets metrics history for a device with pagination.
    /// </summary>
    Task<(IReadOnlyCollection<DeviceMetrics> Metrics, int TotalCount)> GetMetricsHistoryAsync(
        DeviceId deviceId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
