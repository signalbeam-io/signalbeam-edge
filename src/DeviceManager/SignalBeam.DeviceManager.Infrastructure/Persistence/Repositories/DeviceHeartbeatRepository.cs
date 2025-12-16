using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceHeartbeat entity (TimescaleDB hypertable).
/// Optimized for time-series queries on heartbeat data.
/// </summary>
public class DeviceHeartbeatRepository : IDeviceHeartbeatRepository
{
    private readonly DeviceDbContext _context;

    public DeviceHeartbeatRepository(DeviceDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await _context.DeviceHeartbeats.AddAsync(heartbeat, cancellationToken);
    }

    public async Task<DeviceHeartbeat?> GetLatestByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceHeartbeat>> GetByDeviceIdAndTimeRangeAsync(
        DeviceId deviceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId && h.Timestamp >= startTime && h.Timestamp <= endTime)
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceHeartbeat>> GetRecentByDeviceIdAsync(
        DeviceId deviceId,
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceHeartbeats
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
