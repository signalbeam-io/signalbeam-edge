using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Events;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to revoke a device certificate.
/// </summary>
public record RevokeCertificateCommand(
    string SerialNumber,
    string? Reason = null);

/// <summary>
/// Response after revoking a certificate.
/// </summary>
public record RevokeCertificateResponse(
    Guid DeviceId,
    string SerialNumber,
    DateTimeOffset RevokedAt,
    string? Reason);

/// <summary>
/// Handler for RevokeCertificateCommand.
/// </summary>
public class RevokeCertificateHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceCertificateRepository _certificateRepository;

    public RevokeCertificateHandler(
        IDeviceRepository deviceRepository,
        IDeviceCertificateRepository certificateRepository)
    {
        _deviceRepository = deviceRepository;
        _certificateRepository = certificateRepository;
    }

    public async Task<Result<RevokeCertificateResponse>> Handle(
        RevokeCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // Find certificate
        var cert = await _certificateRepository.GetBySerialNumberAsync(
            command.SerialNumber,
            cancellationToken);

        if (cert == null)
        {
            var error = Error.NotFound(
                "CERTIFICATE_NOT_FOUND",
                $"Certificate with serial number {command.SerialNumber} not found.");
            return Result.Failure<RevokeCertificateResponse>(error);
        }

        // Revoke certificate
        try
        {
            var revokedAt = DateTimeOffset.UtcNow;
            cert.Revoke(revokedAt);

            // Save changes
            _certificateRepository.Update(cert);
            await _certificateRepository.SaveChangesAsync(cancellationToken);

            // TODO: Consider raising domain event when event handling infrastructure is ready
            // Event would notify other services about certificate revocation

            return Result<RevokeCertificateResponse>.Success(new RevokeCertificateResponse(
                cert.DeviceId.Value,
                cert.SerialNumber,
                revokedAt,
                command.Reason));
        }
        catch (InvalidOperationException ex)
        {
            var error = Error.Validation("REVOCATION_FAILED", ex.Message);
            return Result.Failure<RevokeCertificateResponse>(error);
        }
    }
}
