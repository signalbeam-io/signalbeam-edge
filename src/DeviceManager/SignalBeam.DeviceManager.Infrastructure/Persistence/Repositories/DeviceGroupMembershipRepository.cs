using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceGroupMembership entity.
/// Manages many-to-many relationships between devices and groups.
/// </summary>
public class DeviceGroupMembershipRepository : IDeviceGroupMembershipRepository
{
    private readonly DeviceDbContext _context;

    public DeviceGroupMembershipRepository(DeviceDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyCollection<DeviceGroupMembership>> GetByGroupIdAsync(
        DeviceGroupId groupId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroupMemberships
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceGroupMembership>> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroupMemberships
            .Where(m => m.DeviceId == deviceId)
            .OrderBy(m => m.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceGroupMembership?> GetByGroupAndDeviceAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, cancellationToken);
    }

    public async Task AddAsync(
        DeviceGroupMembership membership,
        CancellationToken cancellationToken = default)
    {
        await _context.DeviceGroupMemberships.AddAsync(membership, cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<DeviceGroupMembership> memberships,
        CancellationToken cancellationToken = default)
    {
        await _context.DeviceGroupMemberships.AddRangeAsync(memberships, cancellationToken);
    }

    public async Task RemoveAsync(
        DeviceGroupMembershipId membershipId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.DeviceGroupMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

        if (membership != null)
        {
            _context.DeviceGroupMemberships.Remove(membership);
        }
    }

    public async Task RemoveByGroupAndDeviceAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.DeviceGroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, cancellationToken);

        if (membership != null)
        {
            _context.DeviceGroupMemberships.Remove(membership);
        }
    }

    public Task RemoveRangeAsync(
        IEnumerable<DeviceGroupMembership> memberships,
        CancellationToken cancellationToken = default)
    {
        _context.DeviceGroupMemberships.RemoveRange(memberships);
        return Task.CompletedTask;
    }

    public async Task RemoveAllByGroupIdAsync(
        DeviceGroupId groupId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _context.DeviceGroupMemberships
            .Where(m => m.GroupId == groupId)
            .ToListAsync(cancellationToken);

        _context.DeviceGroupMemberships.RemoveRange(memberships);
    }

    public async Task<bool> ExistsAsync(
        DeviceGroupId groupId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceGroupMemberships
            .AnyAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
