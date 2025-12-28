using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a client certificate for device mTLS authentication (optional).
/// </summary>
public class DeviceCertificate : Entity<Guid>
{
    /// <summary>
    /// The device this certificate belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// PEM-encoded certificate.
    /// </summary>
    public string Certificate { get; private set; } = string.Empty;

    /// <summary>
    /// Certificate serial number.
    /// </summary>
    public string SerialNumber { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 fingerprint of the certificate.
    /// </summary>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>
    /// Subject of the certificate (e.g., CN=device-{deviceId}).
    /// </summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>
    /// Certificate type (Device, RootCA, IntermediateCA).
    /// </summary>
    public CertificateType Type { get; private set; }

    /// <summary>
    /// When the certificate was issued.
    /// </summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>
    /// When the certificate expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the certificate was revoked (null = not revoked).
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Whether this certificate is currently valid (not revoked and not expired).
    /// </summary>
    public bool IsValid => RevokedAt == null && ExpiresAt > DateTimeOffset.UtcNow;

    // EF Core constructor
    private DeviceCertificate() : base(Guid.NewGuid())
    {
    }

    private DeviceCertificate(
        DeviceId deviceId,
        string certificate,
        string serialNumber,
        string fingerprint,
        string subject,
        CertificateType type,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) : base(Guid.NewGuid())
    {
        DeviceId = deviceId;
        Certificate = certificate;
        SerialNumber = serialNumber;
        Fingerprint = fingerprint;
        Subject = subject;
        Type = type;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Factory method to create a new device certificate.
    /// </summary>
    public static DeviceCertificate Create(
        DeviceId deviceId,
        string certificate,
        string serialNumber,
        string fingerprint,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? subject = null,
        CertificateType type = CertificateType.Device)
    {
        if (string.IsNullOrWhiteSpace(certificate))
            throw new ArgumentException("Certificate cannot be empty.", nameof(certificate));

        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("Serial number cannot be empty.", nameof(serialNumber));

        if (string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("Fingerprint cannot be empty.", nameof(fingerprint));

        if (expiresAt <= issuedAt)
            throw new ArgumentException("Certificate expiration must be after issuance.", nameof(expiresAt));

        var certSubject = subject ?? $"CN=device-{deviceId.Value}";

        return new DeviceCertificate(deviceId, certificate, serialNumber, fingerprint, certSubject, type, issuedAt, expiresAt);
    }

    /// <summary>
    /// Revokes this certificate.
    /// </summary>
    public void Revoke(DateTimeOffset timestamp)
    {
        if (RevokedAt != null)
            throw new InvalidOperationException("Certificate is already revoked.");

        RevokedAt = timestamp;
    }

    /// <summary>
    /// Checks if the certificate is expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset currentTime)
    {
        return ExpiresAt <= currentTime;
    }

    /// <summary>
    /// Checks if certificate is eligible for renewal (less than renewalThresholdDays until expiration).
    /// </summary>
    public bool IsEligibleForRenewal(DateTimeOffset currentTime, int renewalThresholdDays = 30)
    {
        if (RevokedAt != null)
            return false; // Cannot renew revoked certificate

        var daysUntilExpiration = (ExpiresAt - currentTime).TotalDays;
        return daysUntilExpiration <= renewalThresholdDays && daysUntilExpiration > 0;
    }

    /// <summary>
    /// Renews the certificate by creating a new instance with extended validity.
    /// The old certificate is revoked automatically.
    /// </summary>
    public static DeviceCertificate Renew(
        DeviceCertificate oldCertificate,
        string newCertificatePem,
        string newSerialNumber,
        string newFingerprint,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        if (oldCertificate == null)
            throw new ArgumentNullException(nameof(oldCertificate));

        if (oldCertificate.RevokedAt != null)
            throw new InvalidOperationException("Cannot renew a revoked certificate.");

        // Create new certificate with same device ID and subject
        var renewed = Create(
            oldCertificate.DeviceId,
            newCertificatePem,
            newSerialNumber,
            newFingerprint,
            issuedAt,
            expiresAt,
            oldCertificate.Subject,
            oldCertificate.Type);

        // Revoke old certificate
        oldCertificate.Revoke(issuedAt);

        return renewed;
    }
}
