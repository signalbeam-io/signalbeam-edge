namespace SignalBeam.DeviceManager.Application.Services;

/// <summary>
/// Service for managing dynamic group memberships.
/// Evaluates tag queries and automatically updates group memberships.
/// </summary>
public interface IDynamicGroupMembershipManager
{
    /// <summary>
    /// Updates memberships for a specific dynamic group.
    /// Evaluates the group's tag query against all devices and syncs memberships.
    /// </summary>
    /// <param name="groupId">ID of the dynamic group to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateDynamicGroupMembershipsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates memberships for all dynamic groups.
    /// Useful for background service periodic updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAllDynamicGroupsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates memberships for all dynamic groups belonging to a specific tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateDynamicGroupsForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
