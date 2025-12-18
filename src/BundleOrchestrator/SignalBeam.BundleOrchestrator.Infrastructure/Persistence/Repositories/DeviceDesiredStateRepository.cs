using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IDeviceDesiredStateRepository.
/// </summary>
public class DeviceDesiredStateRepository : IDeviceDesiredStateRepository
{
    private readonly BundleDbContext _context;

    public DeviceDesiredStateRepository(BundleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DeviceDesiredState?> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceDesiredStates
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceDesiredState>> GetByBundleIdAsync(
        BundleId bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceDesiredStates
            .Where(d => d.BundleId == bundleId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeviceDesiredState desiredState, CancellationToken cancellationToken = default)
    {
        await _context.DeviceDesiredStates.AddAsync(desiredState, cancellationToken);
    }

    public Task UpdateAsync(DeviceDesiredState desiredState, CancellationToken cancellationToken = default)
    {
        _context.DeviceDesiredStates.Update(desiredState);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        var desiredState = await GetByDeviceIdAsync(deviceId, cancellationToken);
        if (desiredState != null)
        {
            _context.DeviceDesiredStates.Remove(desiredState);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
