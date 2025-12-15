using SignalBeam.Domain.Entities;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository for DeviceMetrics aggregate.
/// </summary>
public interface IDeviceMetricsRepository
{
    Task AddAsync(DeviceMetrics metrics, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
