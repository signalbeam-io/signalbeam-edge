using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Cryptography;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// Certificate Authority service for issuing and managing device certificates.
/// Delegates key storage to ICaKeyStore (in-memory for dev, Azure Key Vault for production).
/// </summary>
public class CertificateAuthorityService : ICertificateAuthorityService
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICaKeyStore _caKeyStore;
    private readonly ILogger<CertificateAuthorityService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private bool _initialized;

    public CertificateAuthorityService(
        ICertificateGenerator certificateGenerator,
        ICaKeyStore caKeyStore,
        ILogger<CertificateAuthorityService> logger)
    {
        _certificateGenerator = certificateGenerator;
        _caKeyStore = caKeyStore;
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

            var caKeyExists = await _caKeyStore.CaKeyExistsAsync(cancellationToken);

            if (!caKeyExists)
            {
                _logger.LogInformation("No existing CA key found, generating new root CA certificate");

                var caCert = _certificateGenerator.GenerateRootCaCertificate(
                    "CN=SignalBeam Root CA, O=SignalBeam, C=US",
                    validityDays: 3650); // 10 years

                await _caKeyStore.StoreCaKeyAsync(
                    caCert.CertificatePem,
                    caCert.PrivateKeyPem,
                    cancellationToken);
            }
            else
            {
                _logger.LogInformation("Existing CA key found in key store");
            }

            _initialized = true;
            _logger.LogInformation("Certificate Authority initialized successfully");
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
        await InitializeAsync(cancellationToken);

        try
        {
            var caCertificatePem = await _caKeyStore.GetCaCertificateAsync(cancellationToken);
            var caPrivateKeyPem = await _caKeyStore.GetCaPrivateKeyAsync(cancellationToken);

            var serialNumber = GenerateSerialNumber();

            var subject = $"CN=device-{deviceId.Value}, O=SignalBeam";
            var deviceCert = _certificateGenerator.GenerateDeviceCertificate(
                subject,
                serialNumber,
                validityDays);

            var signedCertPem = _certificateGenerator.SignCertificate(
                deviceCert.CertificatePem,
                caPrivateKeyPem,
                caCertificatePem);

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
                caCertificatePem,
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

    public async Task<Result<IssuedCertificate>> SignCsrAsync(
        DeviceId deviceId,
        string csrPem,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            var caCertificatePem = await _caKeyStore.GetCaCertificateAsync(cancellationToken);
            var caPrivateKeyPem = await _caKeyStore.GetCaPrivateKeyAsync(cancellationToken);

            var serialNumber = GenerateSerialNumber();

            var signedCertPem = _certificateGenerator.SignCertificateSigningRequest(
                csrPem,
                caPrivateKeyPem,
                caCertificatePem,
                serialNumber,
                validityDays);

            var fingerprint = _certificateGenerator.CalculateFingerprint(signedCertPem);
            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddDays(validityDays);

            _logger.LogInformation(
                "Signed CSR for device {DeviceId}. Serial: {SerialNumber}, Expires: {ExpiresAt}",
                deviceId.Value, serialNumber, expiresAt);

            // The device holds its own private key — never returned here.
            return Result<IssuedCertificate>.Success(new IssuedCertificate(
                signedCertPem,
                string.Empty,
                caCertificatePem,
                serialNumber,
                fingerprint,
                issuedAt,
                expiresAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign CSR for device {DeviceId}", deviceId.Value);
            return Result.Failure<IssuedCertificate>(Error.Failure(
                "CSR_SIGNING_FAILED",
                $"Failed to sign certificate signing request: {ex.Message}"));
        }
    }

    public async Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return await _caKeyStore.GetCaCertificateAsync(cancellationToken);
    }

    private static string GenerateSerialNumber()
    {
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
