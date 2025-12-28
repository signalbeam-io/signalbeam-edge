using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get all certificates for a device.
/// </summary>
public record GetDeviceCertificatesQuery(Guid DeviceId);

/// <summary>
/// DTO representing a device certificate.
/// </summary>
public record DeviceCertificateDto(
    string SerialNumber,
    string Fingerprint,
    string Subject,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool IsValid,
    DateTimeOffset? RevokedAt);

/// <summary>
/// Response containing list of device certificates.
/// </summary>
public record GetDeviceCertificatesResponse(
    Guid DeviceId,
    IReadOnlyList<DeviceCertificateDto> Certificates);

/// <summary>
/// Handler for GetDeviceCertificatesQuery.
/// </summary>
public class GetDeviceCertificatesHandler
{
    private readonly IDeviceCertificateRepository _certificateRepository;

    public GetDeviceCertificatesHandler(IDeviceCertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
    }

    public async Task<Result<GetDeviceCertificatesResponse>> Handle(
        GetDeviceCertificatesQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);
        var certificates = await _certificateRepository.GetByDeviceIdAsync(deviceId, cancellationToken);

        var certificateDtos = certificates
            .Select(c => new DeviceCertificateDto(
                c.SerialNumber,
                c.Fingerprint,
                c.Subject,
                c.IssuedAt,
                c.ExpiresAt,
                c.IsValid,
                c.RevokedAt))
            .ToList();

        return Result<GetDeviceCertificatesResponse>.Success(
            new GetDeviceCertificatesResponse(query.DeviceId, certificateDtos));
    }
}
