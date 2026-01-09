using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for device heartbeat time-series data.
/// </summary>
public interface IDeviceHeartbeatRepository
{
    /// <summary>
    /// Adds a heartbeat record to the time-series database.
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
    /// Gets all devices that haven't sent a heartbeat within the specified threshold.
    /// </summary>
    Task<IReadOnlyCollection<DeviceId>> GetStaleDevicesAsync(
        TimeSpan threshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unique device IDs that have sent heartbeats since the specified time.
    /// </summary>
    Task<IReadOnlyList<DeviceId>> GetActiveDeviceIdsAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes heartbeats older than the specified date for specific devices.
    /// </summary>
    /// <param name="deviceIds">The device IDs to delete heartbeats for.</param>
    /// <param name="olderThan">Delete heartbeats older than this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records deleted.</returns>
    Task<int> DeleteOldHeartbeatsAsync(IEnumerable<DeviceId> deviceIds, DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
