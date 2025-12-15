using SignalBeam.DeviceManager.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Device aggregate.
/// Implements both command and query repository interfaces.
/// </summary>
public class DeviceRepository : IDeviceRepository, IDeviceQueryRepository
{
    private readonly DeviceDbContext _context;

    public DeviceRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _context.Devices.AddAsync(device, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Device> Devices, int TotalCount)> GetDevicesAsync(
        Guid? tenantId,
        DeviceStatus? status,
        string? tag,
        Guid? deviceGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Devices.AsQueryable();

        // Apply filters
        if (tenantId.HasValue)
        {
            var tenantIdValue = new TenantId(tenantId.Value);
            query = query.Where(d => d.TenantId == tenantIdValue);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            // Note: This requires proper JSONB querying in PostgreSQL
            // For simplicity, we're doing client-side filtering here
            // In production, you'd use EF.Functions.JsonContains or raw SQL
            var devicesWithTag = await query.ToListAsync(cancellationToken);
            devicesWithTag = devicesWithTag.Where(d => d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
            query = devicesWithTag.AsQueryable();
        }

        if (deviceGroupId.HasValue)
        {
            var groupId = new DeviceGroupId(deviceGroupId.Value);
            query = query.Where(d => d.DeviceGroupId == groupId);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var devices = await query
            .OrderByDescending(d => d.RegisteredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (devices, totalCount);
    }
}
