using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Http;
using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Cryptography.X509Certificates;

namespace SignalBeam.DeviceManager.Infrastructure.Authentication;

/// <summary>
/// Validates device client certificates for mTLS authentication.
/// </summary>
public class DeviceCertificateValidator : IDeviceCertificateValidator
{
    private readonly DeviceDbContext _context;
    private readonly ICertificateAuthorityService _caService;
    private readonly IHttpContextInfoProvider _httpContextInfo;
    private readonly ILogger<DeviceCertificateValidator> _logger;

    public DeviceCertificateValidator(
        DeviceDbContext context,
        ICertificateAuthorityService caService,
        IHttpContextInfoProvider httpContextInfo,
        ILogger<DeviceCertificateValidator> logger)
    {
        _context = context;
        _caService = caService;
        _httpContextInfo = httpContextInfo;
        _logger = logger;
    }

    public async Task<Result<DeviceAuthenticationResult>> ValidateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Validate certificate expiration
        if (certificate.NotAfter < DateTime.UtcNow)
        {
            await LogAuthenticationFailureAsync(
                null,
                "Certificate expired",
                certificate.Thumbprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Unauthorized("CERTIFICATE_EXPIRED", "The client certificate has expired."));
        }

        if (certificate.NotBefore > DateTime.UtcNow)
        {
            await LogAuthenticationFailureAsync(
                null,
                "Certificate not yet valid",
                certificate.Thumbprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Unauthorized("CERTIFICATE_NOT_YET_VALID", "The client certificate is not yet valid."));
        }

        // 2. Validate certificate chain (verify it was issued by our CA)
        var caCertPem = await _caService.GetCaCertificateAsync(cancellationToken);
        var caCert = X509Certificate2.CreateFromPem(caCertPem);

        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(caCert);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // We handle revocation in DB

        if (!chain.Build(certificate))
        {
            await LogAuthenticationFailureAsync(
                null,
                "Certificate chain validation failed",
                certificate.Thumbprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Unauthorized("INVALID_CERTIFICATE_CHAIN", "Certificate was not issued by trusted CA."));
        }

        // 3. Find certificate in database by fingerprint (SHA-256 thumbprint)
        var fingerprint = certificate.Thumbprint; // SHA-1 thumbprint in .NET
        var storedCert = await _context.DeviceCertificates
            .Where(c => c.Fingerprint == fingerprint)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedCert == null)
        {
            await LogAuthenticationFailureAsync(
                null,
                "Certificate not found in database",
                fingerprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Unauthorized("CERTIFICATE_NOT_FOUND", "Certificate is not registered."));
        }

        // 4. Check if certificate is revoked
        if (storedCert.RevokedAt != null)
        {
            await LogAuthenticationFailureAsync(
                storedCert.DeviceId,
                "Certificate revoked",
                fingerprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Unauthorized("CERTIFICATE_REVOKED", "The certificate has been revoked."));
        }

        // 5. Get device and check status
        var device = await _context.Devices
            .Where(d => d.Id == storedCert.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            await LogAuthenticationFailureAsync(
                storedCert.DeviceId,
                "Device not found",
                fingerprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.NotFound("DEVICE_NOT_FOUND", "Device associated with certificate not found."));
        }

        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            await LogAuthenticationFailureAsync(
                storedCert.DeviceId,
                "Device not approved",
                fingerprint,
                cancellationToken);

            return Result.Failure<DeviceAuthenticationResult>(
                Error.Forbidden("DEVICE_NOT_APPROVED", "Device is not approved for access."));
        }

        // 6. Log successful authentication
        await LogAuthenticationSuccessAsync(storedCert.DeviceId, fingerprint, cancellationToken);

        return Result<DeviceAuthenticationResult>.Success(new DeviceAuthenticationResult(
            device.Id.Value,
            device.TenantId.Value,
            true,
            device.Status.ToString()));
    }

    private async Task LogAuthenticationSuccessAsync(
        Domain.ValueObjects.DeviceId deviceId,
        string certificateFingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = _httpContextInfo.GetClientIpAddress();
            var userAgent = _httpContextInfo.GetUserAgent();

            var log = DeviceAuthenticationLog.LogSuccess(
                deviceId,
                ipAddress,
                userAgent,
                DateTimeOffset.UtcNow,
                apiKeyPrefix: certificateFingerprint[..Math.Min(8, certificateFingerprint.Length)]);

            await _context.DeviceAuthenticationLogs.AddAsync(log, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log successful certificate authentication");
        }
    }

    private async Task LogAuthenticationFailureAsync(
        Domain.ValueObjects.DeviceId? deviceId,
        string failureReason,
        string certificateFingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = _httpContextInfo.GetClientIpAddress();
            var userAgent = _httpContextInfo.GetUserAgent();

            var log = DeviceAuthenticationLog.LogFailure(
                deviceId,
                ipAddress,
                userAgent,
                DateTimeOffset.UtcNow,
                failureReason,
                apiKeyPrefix: certificateFingerprint[..Math.Min(8, certificateFingerprint.Length)]);

            await _context.DeviceAuthenticationLogs.AddAsync(log, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log certificate authentication failure");
        }
    }
}
