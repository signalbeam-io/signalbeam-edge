using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to sign a device-generated PKCS#10 CSR and issue an mTLS client certificate.
/// The device keeps its private key, so the response carries no private key.
/// </summary>
public record SignDeviceCsrCommand(
    Guid DeviceId,
    string Csr,
    int ValidityDays = 90);

public record SignDeviceCsrResponse(
    Guid DeviceId,
    string CertificatePem,
    string CaCertificatePem,
    string SerialNumber,
    string Fingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public class SignDeviceCsrHandler
{
    private const int MaxValidityDays = 365;

    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCertificateRepository _certificateRepository;
    private readonly ICertificateAuthorityService _caService;

    public SignDeviceCsrHandler(
        IDeviceRepository deviceRepository,
        IDeviceCertificateRepository certificateRepository,
        ICertificateAuthorityService caService)
    {
        _deviceRepository = deviceRepository;
        _certificateRepository = certificateRepository;
        _caService = caService;
    }

    public async Task<Result<SignDeviceCsrResponse>> Handle(
        SignDeviceCsrCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Csr))
        {
            return Result.Failure<SignDeviceCsrResponse>(Error.Validation(
                "CSR_REQUIRED", "A certificate signing request (CSR) is required."));
        }

        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            return Result.Failure<SignDeviceCsrResponse>(Error.NotFound(
                "DEVICE_NOT_FOUND", $"Device with ID {command.DeviceId} not found."));
        }

        if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        {
            return Result.Failure<SignDeviceCsrResponse>(Error.Forbidden(
                "DEVICE_NOT_APPROVED", "Only approved devices can receive certificates."));
        }

        var existingCert = await _certificateRepository.GetActiveByDeviceIdAsync(deviceId, cancellationToken);
        if (existingCert is not null)
        {
            return Result.Failure<SignDeviceCsrResponse>(Error.Conflict(
                "CERTIFICATE_ALREADY_EXISTS",
                $"Device {command.DeviceId} already has an active certificate. Use renew instead."));
        }

        // Clamp caller-supplied validity to a sane bound so a device can't request a long-lived cert.
        var validityDays = Math.Clamp(command.ValidityDays, 1, MaxValidityDays);

        var certificateResult = await _caService.SignCsrAsync(
            deviceId, command.Csr, validityDays, cancellationToken);

        if (certificateResult.IsFailure)
        {
            return Result.Failure<SignDeviceCsrResponse>(certificateResult.Error!);
        }

        var cert = certificateResult.Value;

        var deviceCert = DeviceCertificate.Create(
            deviceId,
            cert.CertificatePem,
            cert.SerialNumber,
            cert.Fingerprint,
            cert.IssuedAt,
            cert.ExpiresAt,
            subject: $"CN=device-{deviceId.Value}, O=SignalBeam",
            type: CertificateType.Device);

        await _certificateRepository.AddAsync(deviceCert, cancellationToken);
        await _certificateRepository.SaveChangesAsync(cancellationToken);

        return Result<SignDeviceCsrResponse>.Success(new SignDeviceCsrResponse(
            device.Id.Value,
            cert.CertificatePem,
            cert.CaCertificatePem,
            cert.SerialNumber,
            cert.Fingerprint,
            cert.IssuedAt,
            cert.ExpiresAt));
    }
}
