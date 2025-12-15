using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

public class DeviceActivityLogRepository : IDeviceActivityLogRepository, IDeviceActivityLogQueryRepository
{
    private readonly DeviceDbContext _context;

    public DeviceActivityLogRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceActivityLog log, CancellationToken cancellationToken = default)
    {
        await _context.DeviceActivityLogs.AddAsync(log, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<DeviceActivityLog> Logs, int TotalCount)> GetActivityLogsAsync(
        DeviceId deviceId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DeviceActivityLogs
            .Where(l => l.DeviceId == deviceId);

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (logs, totalCount);
    }
}
