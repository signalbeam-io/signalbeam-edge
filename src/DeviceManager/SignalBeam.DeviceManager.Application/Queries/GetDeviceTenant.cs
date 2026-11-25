using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDeviceTenantQuery(Guid DeviceId);

public record GetDeviceTenantResponse(Guid DeviceId, Guid TenantId);

// Service-to-service lookup used by TelemetryProcessor to attribute alerts to a tenant.
public class GetDeviceTenantHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public GetDeviceTenantHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetDeviceTenantResponse>> Handle(GetDeviceTenantQuery query, CancellationToken cancellationToken)
    {
        var device = await _deviceRepository.GetByIdAsync(new DeviceId(query.DeviceId), cancellationToken);

        return device is null
            ? Result.Failure<GetDeviceTenantResponse>(
                Error.NotFound("DEVICE_NOT_FOUND", $"Device with ID {query.DeviceId} was not found."))
            : Result<GetDeviceTenantResponse>.Success(new GetDeviceTenantResponse(device.Id.Value, device.TenantId.Value));
    }
}
