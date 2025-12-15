using SignalBeam.DeviceManager.Application.Repositories;
using Microsoft.AspNetCore.Mvc;
using SignalBeam.Domain.Enums;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

public record GetDevicesByGroupQuery(
    [FromRoute] Guid DeviceGroupId,
    [FromQuery] DeviceStatus? Status = null,
    [FromQuery] int PageNumber = 1,
    [FromQuery] int PageSize = 20);

public record GetDevicesByGroupResponse(
    IReadOnlyCollection<DeviceResponse> Devices,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for retrieving devices by group with optional filters and pagination.
/// </summary>
public class GetDevicesByGroupHandler
{
    private readonly IDeviceQueryRepository _queryRepository;

    public GetDevicesByGroupHandler(IDeviceQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<Result<GetDevicesByGroupResponse>> Handle(
        GetDevicesByGroupQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PageNumber < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            var error = Error.Validation(
                "INVALID_PAGINATION",
                "Page number must be >= 1 and page size must be between 1 and 100.");
            return Result.Failure<GetDevicesByGroupResponse>(error);
        }

        var (devices, totalCount) = await _queryRepository.GetDevicesAsync(
            tenantId: null,
            status: query.Status,
            tag: null,
            deviceGroupId: query.DeviceGroupId,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        var deviceResponses = devices.Select(d => new DeviceResponse(
            Id: d.Id.Value,
            TenantId: d.TenantId.Value,
            Name: d.Name,
            Status: d.Status.ToString(),
            LastSeenAt: d.LastSeenAt,
            RegisteredAt: d.RegisteredAt,
            Metadata: d.Metadata,
            Tags: d.Tags,
            AssignedBundleId: d.AssignedBundleId?.Value,
            BundleDeploymentStatus: d.BundleDeploymentStatus?.ToString(),
            DeviceGroupId: d.DeviceGroupId?.Value
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return Result<GetDevicesByGroupResponse>.Success(new GetDevicesByGroupResponse(
            Devices: deviceResponses,
            TotalCount: totalCount,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize,
            TotalPages: totalPages));
    }
}
