using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceRegistrationToken.
/// </summary>
public class DeviceRegistrationTokenRepository : IDeviceRegistrationTokenRepository
{
    private readonly DeviceDbContext _context;

    public DeviceRegistrationTokenRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceRegistrationToken?> GetByIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceRegistrationTokens
            .Where(t => t.Id == tokenId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeviceRegistrationToken?> GetByPrefixAsync(string tokenPrefix, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceRegistrationTokens
            .Where(t => t.TokenPrefix == tokenPrefix)
            .Where(t => !t.IsUsed)
            .Where(t => t.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRegistrationToken>> GetValidTokensByTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceRegistrationTokens
            .Where(t => t.TenantId == tenantId)
            .Where(t => !t.IsUsed)
            .Where(t => t.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeviceRegistrationToken token, CancellationToken cancellationToken = default)
    {
        await _context.DeviceRegistrationTokens.AddAsync(token, cancellationToken);
    }

    public Task UpdateAsync(DeviceRegistrationToken token, CancellationToken cancellationToken = default)
    {
        _context.DeviceRegistrationTokens.Update(token);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
