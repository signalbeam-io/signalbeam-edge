using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to add a tag to all devices in a group.
/// </summary>
public record BulkAddDeviceTagsCommand(
    Guid TenantId,
    Guid DeviceGroupId,
    string Tag);

/// <summary>
/// Response after bulk adding tags.
/// </summary>
public record BulkAddDeviceTagsResponse(
    Guid DeviceGroupId,
    string Tag,
    int DevicesUpdated,
    DateTimeOffset CompletedAt);

/// <summary>
/// Handler for BulkAddDeviceTagsCommand.
/// </summary>
public class BulkAddDeviceTagsHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;
    private readonly IDeviceRepository _deviceRepository;

    public BulkAddDeviceTagsHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceGroupMembershipRepository membershipRepository,
        IDeviceRepository deviceRepository)
    {
        _groupRepository = groupRepository;
        _membershipRepository = membershipRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<BulkAddDeviceTagsResponse>> Handle(
        BulkAddDeviceTagsCommand command,
        CancellationToken cancellationToken)
    {
        var deviceGroupId = new DeviceGroupId(command.DeviceGroupId);
        var tenantId = new TenantId(command.TenantId);

        // Verify group exists and belongs to tenant
        var deviceGroup = await _groupRepository.GetByIdAsync(deviceGroupId, cancellationToken);
        if (deviceGroup is null)
        {
            var error = Error.NotFound(
                "DEVICE_GROUP_NOT_FOUND",
                $"Device group with ID {command.DeviceGroupId} was not found.");
            return Result.Failure<BulkAddDeviceTagsResponse>(error);
        }

        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to modify this device group.");
            return Result.Failure<BulkAddDeviceTagsResponse>(error);
        }

        // Validate tag format
        try
        {
            SignalBeam.Domain.ValueObjects.DeviceTag.Create(command.Tag);
        }
        catch (ArgumentException ex)
        {
            var error = Error.Validation(
                "INVALID_TAG_FORMAT",
                ex.Message);
            return Result.Failure<BulkAddDeviceTagsResponse>(error);
        }

        // Get all devices in the group
        var memberships = await _membershipRepository.GetByGroupIdAsync(deviceGroupId, cancellationToken);
        var deviceIds = memberships.Select(m => m.DeviceId).ToList();

        if (deviceIds.Count == 0)
        {
            // No devices in group, operation successful but no updates
            return Result<BulkAddDeviceTagsResponse>.Success(new BulkAddDeviceTagsResponse(
                DeviceGroupId: command.DeviceGroupId,
                Tag: command.Tag,
                DevicesUpdated: 0,
                CompletedAt: DateTimeOffset.UtcNow));
        }

        // Add tag to each device
        int devicesUpdated = 0;
        foreach (var deviceId in deviceIds)
        {
            var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
            if (device is not null && device.TenantId == tenantId)
            {
                device.AddTag(command.Tag);
                devicesUpdated++;
            }
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<BulkAddDeviceTagsResponse>.Success(new BulkAddDeviceTagsResponse(
            DeviceGroupId: command.DeviceGroupId,
            Tag: command.Tag,
            DevicesUpdated: devicesUpdated,
            CompletedAt: DateTimeOffset.UtcNow));
    }
}
