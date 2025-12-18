using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for device aggregate (read/write operations).
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// Gets a device by ID.
    /// </summary>
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a device.
    /// </summary>
    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
