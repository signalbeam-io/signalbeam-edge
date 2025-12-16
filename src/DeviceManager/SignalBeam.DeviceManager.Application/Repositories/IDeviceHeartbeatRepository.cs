using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository interface for DeviceHeartbeat operations (time-series data).
/// </summary>
public interface IDeviceHeartbeatRepository
{
    /// <summary>
    /// Adds a new device heartbeat to the repository.
    /// </summary>
    Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent heartbeat for a device.
    /// </summary>
    Task<DeviceHeartbeat?> GetLatestByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets heartbeats for a device within a time range.
    /// </summary>
    Task<IReadOnlyCollection<DeviceHeartbeat>> GetByDeviceIdAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent heartbeats for a device (last N records).
    /// </summary>
    Task<IReadOnlyCollection<DeviceHeartbeat>> GetRecentByDeviceIdAsync(
        DeviceId deviceId,
        int count = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
