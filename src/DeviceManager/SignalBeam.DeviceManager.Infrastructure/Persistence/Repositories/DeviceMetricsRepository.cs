using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

public class DeviceMetricsRepository : IDeviceMetricsRepository
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
}
