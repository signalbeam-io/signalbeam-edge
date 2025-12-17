using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

public class DeviceMetricsRepository : IDeviceMetricsRepository, IDeviceMetricsQueryRepository
{
    private readonly DeviceDbContext _context;

    public DeviceMetricsRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceMetrics metrics, CancellationToken cancellationToken = default)
    {
        await _context.DeviceMetrics.AddAsync(metrics, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<DeviceMetrics> Metrics, int TotalCount)> GetMetricsHistoryAsync(
        DeviceId deviceId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DeviceMetrics
            .Where(m => m.DeviceId == deviceId);

        if (startDate.HasValue)
        {
            query = query.Where(m => m.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.Timestamp <= endDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var metrics = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (metrics, totalCount);
    }
}
