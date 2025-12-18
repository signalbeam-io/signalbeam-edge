using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for DeviceDesiredState aggregate.
/// </summary>
public interface IDeviceDesiredStateRepository
{
    /// <summary>
    /// Gets the desired state for a device.
    /// </summary>
    Task<DeviceDesiredState?> GetByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all devices with a specific bundle assigned.
    /// </summary>
    Task<IReadOnlyList<DeviceDesiredState>> GetByBundleIdAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new desired state.
    /// </summary>
    Task AddAsync(DeviceDesiredState desiredState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing desired state.
    /// </summary>
    Task UpdateAsync(DeviceDesiredState desiredState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a desired state.
    /// </summary>
    Task DeleteAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
