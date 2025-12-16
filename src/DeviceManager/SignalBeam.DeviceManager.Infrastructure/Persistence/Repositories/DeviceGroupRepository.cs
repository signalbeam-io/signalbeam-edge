using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceGroup entity.
/// </summary>
public class DeviceGroupRepository : IDeviceGroupRepository
{
    private readonly DeviceDbContext _context;

    public DeviceGroupRepository(DeviceDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(DeviceGroup deviceGroup, CancellationToken cancellationToken = default)
    {
        await _context.DeviceGroups.AddAsync(deviceGroup, cancellationToken);
    }

    public Task UpdateAsync(DeviceGroup deviceGroup, CancellationToken cancellationToken = default)
    {
        _context.DeviceGroups.Update(deviceGroup);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(DeviceGroupId deviceGroupId, CancellationToken cancellationToken = default)
    {
        var deviceGroup = await _context.DeviceGroups
            .FirstOrDefaultAsync(g => g.Id == deviceGroupId, cancellationToken);

        if (deviceGroup != null)
        {
            _context.DeviceGroups.Remove(deviceGroup);
        }
    }

    public async Task<DeviceGroup?> GetByIdAsync(DeviceGroupId deviceGroupId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroups
            .FirstOrDefaultAsync(g => g.Id == deviceGroupId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceGroup>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroups
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroups
            .AnyAsync(g => g.TenantId == tenantId && g.Name == name, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
