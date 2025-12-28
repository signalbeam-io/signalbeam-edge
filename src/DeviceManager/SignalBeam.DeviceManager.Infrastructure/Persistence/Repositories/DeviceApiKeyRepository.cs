using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceApiKey entity.
/// </summary>
public class DeviceApiKeyRepository : IDeviceApiKeyRepository
{
    private readonly DeviceDbContext _context;

    public DeviceApiKeyRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceApiKeys
            .Where(k => k.Id == apiKeyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeviceApiKey?> GetActiveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.DeviceApiKeys
            .Where(k => k.KeyPrefix == keyPrefix)
            .Where(k => k.RevokedAt == null) // Not revoked
            .Where(k => k.ExpiresAt == null || k.ExpiresAt > now) // Not expired
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceApiKey>> GetActiveByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.DeviceApiKeys
            .Where(k => k.DeviceId == deviceId)
            .Where(k => k.RevokedAt == null) // Not revoked
            .Where(k => k.ExpiresAt == null || k.ExpiresAt > now) // Not expired
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeviceApiKey apiKey, CancellationToken cancellationToken = default)
    {
        await _context.DeviceApiKeys.AddAsync(apiKey, cancellationToken);
    }

    public void Update(DeviceApiKey apiKey)
    {
        _context.DeviceApiKeys.Update(apiKey);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
