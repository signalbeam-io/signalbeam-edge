using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Cryptography;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// Certificate Authority service for issuing and managing device certificates.
/// Simplified MVP version - stores CA key in memory/file.
/// TODO: Enhance with Azure Key Vault for production.
/// </summary>
public class CertificateAuthorityService : ICertificateAuthorityService
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ILogger<CertificateAuthorityService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private bool _initialized;
    private string? _caCertificatePem;
    private string? _caPrivateKeyPem;

    public CertificateAuthorityService(
        ICertificateGenerator certificateGenerator,
        ILogger<CertificateAuthorityService> logger)
    {
        _certificateGenerator = certificateGenerator;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing Certificate Authority...");

            // For MVP: Generate CA certificate on startup (in-memory)
            // TODO: In production, load from Azure Key Vault or secure storage
            var caCert = _certificateGenerator.GenerateRootCaCertificate(
                "CN=SignalBeam Root CA, O=SignalBeam, C=US",
                validityDays: 3650); // 10 years

            _caCertificatePem = caCert.CertificatePem;
            _caPrivateKeyPem = caCert.PrivateKeyPem;

            _initialized = true;

            _logger.LogInformation("Certificate Authority initialized successfully");
            _logger.LogWarning(
                "SECURITY WARNING: CA private key is stored in memory. " +
                "For production, integrate with Azure Key Vault.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<Result<IssuedCertificate>> IssueCertificateAsync(
        DeviceId deviceId,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        // Ensure CA is initialized
        await InitializeAsync(cancellationToken);

        if (_caCertificatePem == null || _caPrivateKeyPem == null)
        {
            var error = Error.Failure(
                "CA_NOT_INITIALIZED",
                "Certificate Authority is not properly initialized.");
            return Result.Failure<IssuedCertificate>(error);
        }

        try
        {
            // Generate unique serial number
            var serialNumber = GenerateSerialNumber();

            // Generate device certificate
            var subject = $"CN=device-{deviceId.Value}, O=SignalBeam";
            var deviceCert = _certificateGenerator.GenerateDeviceCertificate(
                subject,
                serialNumber,
                validityDays);

            // Sign the device certificate with CA private key
            var signedCertPem = _certificateGenerator.SignCertificate(
                deviceCert.CertificatePem,
                _caPrivateKeyPem,
                _caCertificatePem);

            // Calculate fingerprint of signed certificate
            var fingerprint = _certificateGenerator.CalculateFingerprint(signedCertPem);

            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddDays(validityDays);

            _logger.LogInformation(
                "Issued certificate for device {DeviceId}. Serial: {SerialNumber}, Expires: {ExpiresAt}",
                deviceId.Value,
                serialNumber,
                expiresAt);

            return Result<IssuedCertificate>.Success(new IssuedCertificate(
                signedCertPem,
                deviceCert.PrivateKeyPem,
                _caCertificatePem,
                serialNumber,
                fingerprint,
                issuedAt,
                expiresAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue certificate for device {DeviceId}", deviceId.Value);

            var error = Error.Failure(
                "CERTIFICATE_GENERATION_FAILED",
                $"Failed to issue certificate: {ex.Message}");
            return Result.Failure<IssuedCertificate>(error);
        }
    }

    public async Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default)
    {
        // Ensure CA is initialized
        await InitializeAsync(cancellationToken);

        if (_caCertificatePem == null)
        {
            throw new InvalidOperationException("CA certificate is not available.");
        }

        return _caCertificatePem;
    }

    private static string GenerateSerialNumber()
    {
        // Generate cryptographically secure random 20-byte serial number
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
