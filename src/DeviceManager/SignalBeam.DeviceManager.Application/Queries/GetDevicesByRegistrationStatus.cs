using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get devices by registration status.
/// </summary>
public record GetDevicesByRegistrationStatusQuery(
    Guid TenantId,
    DeviceRegistrationStatus Status,
    int PageNumber = 1,
    int PageSize = 50);

/// <summary>
/// Response containing devices with specified registration status.
/// </summary>
public record GetDevicesByRegistrationStatusResponse(
    List<DeviceStatusDto> Devices,
    int TotalCount,
    int PageNumber,
    int PageSize);

/// <summary>
/// DTO for device with registration status information.
/// </summary>
public record DeviceStatusDto(
    Guid DeviceId,
    string Name,
    string Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? LastSeenAt,
    string? Metadata);

/// <summary>
/// Handler for GetDevicesByRegistrationStatusQuery.
/// </summary>
public class GetDevicesByRegistrationStatusHandler
{
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetDevicesByRegistrationStatusHandler(IDeviceQueryRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<GetDevicesByRegistrationStatusResponse>> Handle(
        GetDevicesByRegistrationStatusQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all devices for the tenant using the existing GetDevicesAsync method
            // We'll get all devices and filter by registration status in memory
            // For better performance, consider adding registration status filter to GetDevicesAsync
            var (allDevices, _) = await _deviceRepository.GetDevicesAsync(
                query.TenantId,
                status: null,
                tag: null,
                deviceGroupId: null,
                pageNumber: 1,
                pageSize: int.MaxValue, // Get all devices first
                cancellationToken);

            // Filter by registration status
            var filteredDevices = allDevices
                .Where(d => d.RegistrationStatus == query.Status)
                .OrderByDescending(d => d.RegisteredAt)
                .ToList();

            var totalCount = filteredDevices.Count;

            // Apply pagination
            var paginatedDevices = filteredDevices
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(d => new DeviceStatusDto(
                    d.Id.Value,
                    d.Name,
                    d.RegistrationStatus.ToString(),
                    d.RegisteredAt,
                    d.LastSeenAt,
                    d.Metadata))
                .ToList();

            return Result<GetDevicesByRegistrationStatusResponse>.Success(
                new GetDevicesByRegistrationStatusResponse(
                    paginatedDevices,
                    totalCount,
                    query.PageNumber,
                    query.PageSize));
        }
        catch (Exception ex)
        {
            return Result.Failure<GetDevicesByRegistrationStatusResponse>(
                Error.Unexpected(
                    "DeviceQuery.Failed",
                    $"Failed to retrieve devices by registration status: {ex.Message}"));
        }
    }
}
