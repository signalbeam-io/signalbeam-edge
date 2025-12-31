using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Queries;

/// <summary>
/// Query to get a device by its ID.
/// </summary>
public record GetDeviceByIdQuery(Guid DeviceId);

/// <summary>
/// Response containing device details.
/// </summary>
public record DeviceResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset RegisteredAt,
    string? Metadata,
    IReadOnlyCollection<string> Tags,
    Guid? AssignedBundleId,
    string? BundleDeploymentStatus,
    Guid? DeviceGroupId);

/// <summary>
/// Type alias for backward compatibility.
/// </summary>
public record GetDeviceByIdResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset RegisteredAt,
    string? Metadata,
    IReadOnlyCollection<string> Tags,
    Guid? AssignedBundleId,
    string? BundleDeploymentStatus,
    Guid? DeviceGroupId) : DeviceResponse(Id, TenantId, Name, Status, LastSeenAt, RegisteredAt, Metadata, Tags, AssignedBundleId, BundleDeploymentStatus, DeviceGroupId);

/// <summary>
/// Handler for GetDeviceByIdQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetDeviceByIdHandler
{
    private readonly IDeviceQueryRepository _deviceRepository;

    public GetDeviceByIdHandler(IDeviceQueryRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<DeviceResponse>> Handle(
        GetDeviceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(query.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {query.DeviceId} was not found.");
            return Result.Failure<DeviceResponse>(error);
        }

        var response = new DeviceResponse(
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
            device.DeviceGroupId?.Value);

        return Result<DeviceResponse>.Success(response);
    }
}
