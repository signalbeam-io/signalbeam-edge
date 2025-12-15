using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get devices with optional filters.
/// </summary>
public record GetDevicesQuery(
    Guid? TenantId = null,
    DeviceStatus? Status = null,
    string? Tag = null,
    Guid? DeviceGroupId = null,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>
/// Paginated response containing devices.
/// </summary>
public record GetDevicesResponse(
    IReadOnlyCollection<DeviceResponse> Devices,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for GetDevicesQuery.
/// </summary>
public class GetDevicesHandler
{
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetDevicesHandler(IDeviceQueryRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetDevicesResponse>> Handle(
        GetDevicesQuery query,
        CancellationToken cancellationToken)
    {
        var (devices, totalCount) = await _deviceRepository.GetDevicesAsync(
            query.TenantId,
            query.Status,
            query.Tag,
            query.DeviceGroupId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var deviceResponses = devices.Select(device => new DeviceResponse(
            device.Id.Value,
            device.TenantId.Value,
            device.Name,
            device.Status.ToString(),
            device.LastSeenAt,
            device.RegisteredAt,
            device.Metadata,
            device.Tags,
            device.AssignedBundleId?.Value,
            device.BundleDeploymentStatus?.ToString(),
            device.DeviceGroupId?.Value)).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var response = new GetDevicesResponse(
            deviceResponses,
            totalCount,
            query.PageNumber,
            query.PageSize,
            totalPages);

        return Result<GetDevicesResponse>.Success(response);
    }
}
