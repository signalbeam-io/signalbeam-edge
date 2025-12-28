using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceAuthenticationLog entity.
/// </summary>
public class DeviceAuthenticationLogRepository : IDeviceAuthenticationLogRepository
{
    private readonly DeviceDbContext _context;

    public DeviceAuthenticationLogRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeviceAuthenticationLog log, CancellationToken cancellationToken = default)
    {
        await _context.DeviceAuthenticationLogs.AddAsync(log, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
