using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Services;

/// <summary>
/// Service for Certificate Authority operations.
/// </summary>
public interface ICertificateAuthorityService
{
    /// <summary>
    /// Issues a new device certificate.
    /// </summary>
    /// <param name="deviceId">The device to issue a certificate for.</param>
    /// <param name="validityDays">Number of days the certificate is valid (default: 90).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issued certificate with private key and CA certificate.</returns>
    Task<Result<IssuedCertificate>> IssueCertificateAsync(
        DeviceId deviceId,
        int validityDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the CA certificate (for distribution to devices).
    /// </summary>
    Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the CA (generates root CA if not exists).
    /// This is called on service startup.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a newly issued certificate.
/// </summary>
public record IssuedCertificate(
    string CertificatePem,
    string PrivateKeyPem,
    string CaCertificatePem,
    string SerialNumber,
    string Fingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
