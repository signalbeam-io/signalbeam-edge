using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Application.Repositories;

/// <summary>
/// Repository interface for DeviceGroup aggregate.
/// </summary>
public interface IDeviceGroupRepository
{
    /// <summary>
    /// Gets a device group by its ID.
    /// </summary>
    Task<DeviceGroup?> GetByIdAsync(DeviceGroupId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all device IDs in a group.
    /// </summary>
    Task<IReadOnlyList<DeviceId>> GetDeviceIdsInGroupAsync(DeviceGroupId groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups for a tenant.
    /// </summary>
    Task<IReadOnlyList<DeviceGroup>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
