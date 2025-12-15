using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository interface for Device aggregate.
/// Follows hexagonal architecture - defined in Application, implemented in Infrastructure.
/// </summary>
public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
