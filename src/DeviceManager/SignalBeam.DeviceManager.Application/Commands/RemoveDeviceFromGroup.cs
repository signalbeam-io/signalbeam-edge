using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to remove a device from a static group.
/// </summary>
public record RemoveDeviceFromGroupCommand(
    Guid TenantId,
    Guid DeviceGroupId,
    Guid DeviceId);

/// <summary>
/// Response after removing a device from a group.
/// </summary>
public record RemoveDeviceFromGroupResponse(
    Guid DeviceGroupId,
    Guid DeviceId,
    DateTimeOffset RemovedAt);

/// <summary>
/// Handler for RemoveDeviceFromGroupCommand.
/// </summary>
public class RemoveDeviceFromGroupHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;

    public RemoveDeviceFromGroupHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceRepository deviceRepository,
        IDeviceGroupMembershipRepository membershipRepository)
    {
        _groupRepository = groupRepository;
        _deviceRepository = deviceRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<Result<RemoveDeviceFromGroupResponse>> Handle(
        RemoveDeviceFromGroupCommand command,
        CancellationToken cancellationToken)
    {
        var deviceGroupId = new DeviceGroupId(command.DeviceGroupId);
        var deviceId = new DeviceId(command.DeviceId);
        var tenantId = new TenantId(command.TenantId);

        // Verify group exists and belongs to tenant
        var deviceGroup = await _groupRepository.GetByIdAsync(deviceGroupId, cancellationToken);
        if (deviceGroup is null)
        {
            var error = Error.NotFound(
                "DEVICE_GROUP_NOT_FOUND",
                $"Device group with ID {command.DeviceGroupId} was not found.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to modify this device group.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        // Only allow removing from static groups
        if (deviceGroup.Type != GroupType.Static)
        {
            var error = Error.Validation(
                "INVALID_GROUP_TYPE",
                "Devices can only be manually removed from static groups. Dynamic group memberships are managed automatically.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        // Verify device exists and belongs to tenant
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        if (device.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_ACCESS_DENIED",
                "You do not have permission to modify this device.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        // Check if membership exists
        var membership = await _membershipRepository.GetByGroupAndDeviceAsync(
            deviceGroupId, deviceId, cancellationToken);

        if (membership is null)
        {
            var error = Error.NotFound(
                "MEMBERSHIP_NOT_FOUND",
                $"Device {command.DeviceId} is not a member of group {command.DeviceGroupId}.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        // Only allow removing static memberships
        if (membership.Type != MembershipType.Static)
        {
            var error = Error.Validation(
                "INVALID_MEMBERSHIP_TYPE",
                "Cannot manually remove dynamic group memberships. Update the group's tag query or device tags instead.");
            return Result.Failure<RemoveDeviceFromGroupResponse>(error);
        }

        // Remove membership
        await _membershipRepository.RemoveAsync(membership.Id, cancellationToken);
        await _membershipRepository.SaveChangesAsync(cancellationToken);

        return Result<RemoveDeviceFromGroupResponse>.Success(new RemoveDeviceFromGroupResponse(
            DeviceGroupId: command.DeviceGroupId,
            DeviceId: command.DeviceId,
            RemovedAt: DateTimeOffset.UtcNow));
    }
}
