using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to remove a tag from all devices in a group.
/// </summary>
public record BulkRemoveDeviceTagsCommand(
    Guid TenantId,
    Guid DeviceGroupId,
    string Tag);

/// <summary>
/// Response after bulk removing tags.
/// </summary>
public record BulkRemoveDeviceTagsResponse(
    Guid DeviceGroupId,
    string Tag,
    int DevicesUpdated,
    DateTimeOffset CompletedAt);

/// <summary>
/// Handler for BulkRemoveDeviceTagsCommand.
/// </summary>
public class BulkRemoveDeviceTagsHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;
    private readonly IDeviceRepository _deviceRepository;

    public BulkRemoveDeviceTagsHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceGroupMembershipRepository membershipRepository,
        IDeviceRepository deviceRepository)
    {
        _groupRepository = groupRepository;
        _membershipRepository = membershipRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<BulkRemoveDeviceTagsResponse>> Handle(
        BulkRemoveDeviceTagsCommand command,
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
            return Result.Failure<BulkRemoveDeviceTagsResponse>(error);
        }

        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to modify this device group.");
            return Result.Failure<BulkRemoveDeviceTagsResponse>(error);
        }

        // Get all devices in the group
        var memberships = await _membershipRepository.GetByGroupIdAsync(deviceGroupId, cancellationToken);
        var deviceIds = memberships.Select(m => m.DeviceId).ToList();

        if (deviceIds.Count == 0)
        {
            // No devices in group, operation successful but no updates
            return Result<BulkRemoveDeviceTagsResponse>.Success(new BulkRemoveDeviceTagsResponse(
                DeviceGroupId: command.DeviceGroupId,
                Tag: command.Tag,
                DevicesUpdated: 0,
                CompletedAt: DateTimeOffset.UtcNow));
        }

        // Remove tag from each device
        int devicesUpdated = 0;
        foreach (var deviceId in deviceIds)
        {
            var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
            if (device is not null && device.TenantId == tenantId)
            {
                device.RemoveTag(command.Tag);
                devicesUpdated++;
            }
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<BulkRemoveDeviceTagsResponse>.Success(new BulkRemoveDeviceTagsResponse(
            DeviceGroupId: command.DeviceGroupId,
            Tag: command.Tag,
            DevicesUpdated: devicesUpdated,
            CompletedAt: DateTimeOffset.UtcNow));
    }
}
