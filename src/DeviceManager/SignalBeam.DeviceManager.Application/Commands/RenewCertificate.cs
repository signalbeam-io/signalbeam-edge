using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Events;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to renew an expiring certificate.
/// </summary>
public record RenewCertificateCommand(string SerialNumber);

/// <summary>
/// Response after renewing a certificate.
/// </summary>
public record RenewCertificateResponse(
    Guid DeviceId,
    string CertificatePem,
    string PrivateKeyPem,
    string NewSerialNumber,
    string NewFingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Handler for RenewCertificateCommand.
/// </summary>
public class RenewCertificateHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCertificateRepository _certificateRepository;
    private readonly ICertificateAuthorityService _caService;

    public RenewCertificateHandler(
        IDeviceRepository deviceRepository,
        IDeviceCertificateRepository certificateRepository,
        ICertificateAuthorityService caService)
    {
        _deviceRepository = deviceRepository;
        _certificateRepository = certificateRepository;
        _caService = caService;
    }

    public async Task<Result<RenewCertificateResponse>> Handle(
        RenewCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // Find existing certificate
        var oldCert = await _certificateRepository.GetBySerialNumberAsync(
            command.SerialNumber,
            cancellationToken);

        if (oldCert == null)
        {
            var error = Error.NotFound(
                "CERTIFICATE_NOT_FOUND",
                $"Certificate with serial number {command.SerialNumber} not found.");
            return Result.Failure<RenewCertificateResponse>(error);
        }

        // Check if eligible for renewal
        if (!oldCert.IsEligibleForRenewal(DateTimeOffset.UtcNow))
        {
            var error = Error.Validation(
                "NOT_ELIGIBLE_FOR_RENEWAL",
                "Certificate is not yet eligible for renewal (must be within 30 days of expiration).");
            return Result.Failure<RenewCertificateResponse>(error);
        }

        // Get device to verify it still exists and is approved
        var device = await _deviceRepository.GetByIdAsync(oldCert.DeviceId, cancellationToken);
        if (device == null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device {oldCert.DeviceId.Value} not found.");
            return Result.Failure<RenewCertificateResponse>(error);
        }

        // Issue new certificate with same validity period (90 days standard)
        var newCertResult = await _caService.IssueCertificateAsync(
            oldCert.DeviceId,
            90, // Standard 90-day validity
            cancellationToken);

        if (newCertResult.IsFailure)
            return Result.Failure<RenewCertificateResponse>(newCertResult.Error!);

        var newCert = newCertResult.Value;

        // Create renewed certificate and revoke old one
        var renewedCert = DeviceCertificate.Renew(
            oldCert,
            newCert.CertificatePem,
            newCert.SerialNumber,
            newCert.Fingerprint,
            newCert.IssuedAt,
            newCert.ExpiresAt);

        // Save both (old revoked, new active)
        _certificateRepository.Update(oldCert);
        await _certificateRepository.AddAsync(renewedCert, cancellationToken);
        await _certificateRepository.SaveChangesAsync(cancellationToken);

        // TODO: Consider raising domain event when event handling infrastructure is ready
        // Event would notify other services about certificate renewal

        return Result<RenewCertificateResponse>.Success(new RenewCertificateResponse(
            oldCert.DeviceId.Value,
            newCert.CertificatePem,
            newCert.PrivateKeyPem,
            newCert.SerialNumber,
            newCert.Fingerprint,
            newCert.IssuedAt,
            newCert.ExpiresAt));
    }
}
