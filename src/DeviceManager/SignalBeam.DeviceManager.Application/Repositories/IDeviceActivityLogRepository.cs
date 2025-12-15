using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository for DeviceActivityLog aggregate.
/// </summary>
public interface IDeviceActivityLogRepository
{
    Task AddAsync(DeviceActivityLog log, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Query repository for DeviceActivityLog.
/// </summary>
public interface IDeviceActivityLogQueryRepository
{
    Task<(IReadOnlyCollection<DeviceActivityLog> Logs, int TotalCount)> GetActivityLogsAsync(
        DeviceId deviceId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
