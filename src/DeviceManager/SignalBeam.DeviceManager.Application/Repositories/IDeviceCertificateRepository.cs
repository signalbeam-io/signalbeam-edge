using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Repository for DeviceCertificate entity.
/// </summary>
public interface IDeviceCertificateRepository
{
    /// <summary>
    /// Gets a certificate by its ID.
    /// </summary>
    Task<DeviceCertificate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a certificate by its serial number.
    /// </summary>
    Task<DeviceCertificate?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a certificate by its fingerprint (SHA-256 thumbprint).
    /// </summary>
    Task<DeviceCertificate?> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all certificates for a device (active and revoked), ordered by IssuedAt descending.
    /// </summary>
    Task<IReadOnlyList<DeviceCertificate>> GetByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active (non-revoked, non-expired) certificate for a device.
    /// </summary>
    Task<DeviceCertificate?> GetActiveByDeviceIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets certificates expiring within the specified number of days.
    /// Only returns non-revoked certificates.
    /// </summary>
    Task<IReadOnlyList<DeviceCertificate>> GetExpiringCertificatesAsync(int daysThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new certificate.
    /// </summary>
    Task AddAsync(DeviceCertificate certificate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a certificate (e.g., for revocation).
    /// </summary>
    void Update(DeviceCertificate certificate);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
