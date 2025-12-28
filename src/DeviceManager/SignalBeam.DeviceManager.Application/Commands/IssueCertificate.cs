using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to issue a new mTLS certificate for an approved device.
/// </summary>
public record IssueCertificateCommand(
    Guid DeviceId,
    int ValidityDays = 90);

/// <summary>
/// Response after issuing a certificate.
/// </summary>
public record IssueCertificateResponse(
    Guid DeviceId,
    string CertificatePem,
    string PrivateKeyPem,
    string CaCertificatePem,
    string SerialNumber,
    string Fingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Handler for IssueCertificateCommand.
/// </summary>
public class IssueCertificateHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCertificateRepository _certificateRepository;
    private readonly ICertificateAuthorityService _caService;

    public IssueCertificateHandler(
        IDeviceRepository deviceRepository,
        IDeviceCertificateRepository certificateRepository,
        ICertificateAuthorityService caService)
    {
        _deviceRepository = deviceRepository;
        _certificateRepository = certificateRepository;
        _caService = caService;
    }

    public async Task<Result<IssueCertificateResponse>> Handle(
        IssueCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // Get device and validate it's approved
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device == null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} not found.");
            return Result.Failure<IssueCertificateResponse>(error);
        }

        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            var error = Error.Forbidden(
                "DEVICE_NOT_APPROVED",
                "Only approved devices can receive certificates.");
            return Result.Failure<IssueCertificateResponse>(error);
        }

        // Check if device already has an active certificate
        var existingCert = await _certificateRepository.GetActiveByDeviceIdAsync(
            deviceId,
            cancellationToken);

        if (existingCert != null)
        {
            var error = Error.Conflict(
                "CERTIFICATE_ALREADY_EXISTS",
                $"Device {command.DeviceId} already has an active certificate. Use renew instead.");
            return Result.Failure<IssueCertificateResponse>(error);
        }

        // Generate certificate via CA service
        var certificateResult = await _caService.IssueCertificateAsync(
            deviceId,
            command.ValidityDays,
            cancellationToken);

        if (certificateResult.IsFailure)
            return Result.Failure<IssueCertificateResponse>(certificateResult.Error!);

        var cert = certificateResult.Value;

        // Create certificate entity
        var deviceCert = DeviceCertificate.Create(
            deviceId,
            cert.CertificatePem,
            cert.SerialNumber,
            cert.Fingerprint,
            cert.IssuedAt,
            cert.ExpiresAt,
            subject: $"CN=device-{deviceId.Value}, O=SignalBeam",
            type: CertificateType.Device);

        // Save to database
        await _certificateRepository.AddAsync(deviceCert, cancellationToken);
        await _certificateRepository.SaveChangesAsync(cancellationToken);

        // TODO: Consider raising domain event when event handling infrastructure is ready
        // Event would notify other services about certificate issuance

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<IssueCertificateResponse>.Success(new IssueCertificateResponse(
            device.Id.Value,
            cert.CertificatePem,
            cert.PrivateKeyPem,
            cert.CaCertificatePem,
            cert.SerialNumber,
            cert.Fingerprint,
            cert.IssuedAt,
            cert.ExpiresAt));
    }
}
