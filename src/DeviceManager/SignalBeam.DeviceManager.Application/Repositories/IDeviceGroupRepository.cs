using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository interface for DeviceGroup write operations.
/// </summary>
public interface IDeviceGroupRepository
{
    /// <summary>
    /// Adds a new device group to the repository.
    /// </summary>
    Task AddAsync(DeviceGroup deviceGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing device group.
    /// </summary>
    Task UpdateAsync(DeviceGroup deviceGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a device group by ID.
    /// </summary>
    Task DeleteAsync(DeviceGroupId deviceGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a device group by ID.
    /// </summary>
    Task<DeviceGroup?> GetByIdAsync(DeviceGroupId deviceGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all device groups for a tenant.
    /// </summary>
    Task<IReadOnlyCollection<DeviceGroup>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a device group with the given name exists for the tenant.
    /// </summary>
    Task<bool> ExistsByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
