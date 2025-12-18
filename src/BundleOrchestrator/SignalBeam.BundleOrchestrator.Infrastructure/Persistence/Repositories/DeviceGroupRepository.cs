using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IDeviceGroupRepository.
/// NOTE: This is a read-only repository that queries DeviceGroup data.
/// In production, this should ideally call the DeviceManager service via HTTP/gRPC
/// instead of directly accessing the database.
/// </summary>
public class DeviceGroupRepository : IDeviceGroupRepository
{
    private readonly BundleDbContext _context;

    public DeviceGroupRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DeviceGroup?> GetByIdAsync(
        DeviceGroupId id,
        CancellationToken cancellationToken = default)
    {
        // TODO: In production, this should call DeviceManager service
        // For now, we're assuming shared database access
        var deviceGroup = await _context.Set<DeviceGroup>()
            .FromSqlRaw("SELECT * FROM device_manager.device_groups WHERE id = {0}", id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return deviceGroup;
    }

    public async Task<IReadOnlyList<DeviceId>> GetDeviceIdsInGroupAsync(
        DeviceGroupId groupId,
        CancellationToken cancellationToken = default)
    {
        // TODO: In production, this should call DeviceManager service
        // For now, we're assuming shared database access
        var deviceIds = await _context.Set<Device>()
            .FromSqlRaw("SELECT * FROM device_manager.devices WHERE device_group_id = {0}", groupId.Value)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        return deviceIds;
    }

    public async Task<IReadOnlyList<DeviceGroup>> GetAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        // TODO: In production, this should call DeviceManager service
        // For now, we're assuming shared database access
        var deviceGroups = await _context.Set<DeviceGroup>()
            .FromSqlRaw("SELECT * FROM device_manager.device_groups WHERE tenant_id = {0}", tenantId.Value)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return deviceGroups;
    }
}
