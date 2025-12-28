using SignalBeam.Domain.Entities;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository for DeviceAuthenticationLog entity.
/// </summary>
public interface IDeviceAuthenticationLogRepository
{
    /// <summary>
    /// Adds a new authentication log entry.
    /// </summary>
    Task AddAsync(DeviceAuthenticationLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
