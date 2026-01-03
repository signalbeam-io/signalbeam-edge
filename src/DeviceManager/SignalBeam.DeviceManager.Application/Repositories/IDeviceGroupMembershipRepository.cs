using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository interface for DeviceGroupMembership operations.
/// Manages many-to-many relationships between devices and groups.
/// </summary>
public interface IDeviceGroupMembershipRepository
{
    /// <summary>
    /// Gets all memberships for a specific device group.
    /// </summary>
    Task<IReadOnlyCollection<DeviceGroupMembership>> GetByGroupIdAsync(
        DeviceGroupId groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all memberships for a specific device.
    /// </summary>
    Task<IReadOnlyCollection<DeviceGroupMembership>> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific membership by group and device.
    /// </summary>
    Task<DeviceGroupMembership?> GetByGroupAndDeviceAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new device group membership.
    /// </summary>
    Task AddAsync(
        DeviceGroupMembership membership,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple device group memberships.
    /// Used for bulk operations and dynamic group updates.
    /// </summary>
    Task AddRangeAsync(
        IEnumerable<DeviceGroupMembership> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific membership by ID.
    /// </summary>
    Task RemoveAsync(
        DeviceGroupMembershipId membershipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific membership by group and device.
    /// </summary>
    Task RemoveByGroupAndDeviceAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple memberships.
    /// Used for bulk operations and dynamic group updates.
    /// </summary>
    Task RemoveRangeAsync(
        IEnumerable<DeviceGroupMembership> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all memberships for a specific group.
    /// Used when deleting a group or resetting dynamic group memberships.
    /// </summary>
    Task RemoveAllByGroupIdAsync(
        DeviceGroupId groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a membership exists for a specific group and device.
    /// </summary>
    Task<bool> ExistsAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
