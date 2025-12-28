using Microsoft.EntityFrameworkCore;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for DeviceCertificate entity.
/// </summary>
public class DeviceCertificateRepository : IDeviceCertificateRepository
{
    private readonly DeviceDbContext _context;

    public DeviceCertificateRepository(DeviceDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceCertificate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCertificates
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<DeviceCertificate?> GetBySerialNumberAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCertificates
            .FirstOrDefaultAsync(c => c.SerialNumber == serialNumber, cancellationToken);
    }

    public async Task<DeviceCertificate?> GetByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCertificates
            .FirstOrDefaultAsync(c => c.Fingerprint == fingerprint, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCertificate>> GetByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeviceCertificates
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceCertificate?> GetActiveByDeviceIdAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.DeviceCertificates
            .Where(c => c.DeviceId == deviceId)
            .Where(c => c.RevokedAt == null) // Not revoked
            .Where(c => c.ExpiresAt > now) // Not expired
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCertificate>> GetExpiringCertificatesAsync(
        int daysThreshold,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expirationDate = now.AddDays(daysThreshold);

        return await _context.DeviceCertificates
            .Where(c => c.RevokedAt == null) // Not revoked
            .Where(c => c.ExpiresAt <= expirationDate && c.ExpiresAt > now) // Expiring within threshold
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeviceCertificate certificate, CancellationToken cancellationToken = default)
    {
        await _context.DeviceCertificates.AddAsync(certificate, cancellationToken);
    }

    public void Update(DeviceCertificate certificate)
    {
        _context.DeviceCertificates.Update(certificate);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
