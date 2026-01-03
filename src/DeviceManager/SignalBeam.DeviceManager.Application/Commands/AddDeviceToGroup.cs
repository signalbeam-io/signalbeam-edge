using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to add a device to a static group.
/// </summary>
public record AddDeviceToGroupCommand(
    Guid TenantId,
    Guid DeviceGroupId,
    Guid DeviceId);

/// <summary>
/// Response after adding a device to a group.
/// </summary>
public record AddDeviceToGroupResponse(
    Guid MembershipId,
    Guid DeviceGroupId,
    Guid DeviceId,
    DateTimeOffset AddedAt);

/// <summary>
/// Handler for AddDeviceToGroupCommand.
/// </summary>
public class AddDeviceToGroupHandler
{
    private readonly IDeviceGroupRepository _groupRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceGroupMembershipRepository _membershipRepository;

    public AddDeviceToGroupHandler(
        IDeviceGroupRepository groupRepository,
        IDeviceRepository deviceRepository,
        IDeviceGroupMembershipRepository membershipRepository)
    {
        _groupRepository = groupRepository;
        _deviceRepository = deviceRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<Result<AddDeviceToGroupResponse>> Handle(
        AddDeviceToGroupCommand command,
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
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to modify this device group.");
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        // Only allow adding to static groups
        if (deviceGroup.Type != GroupType.Static)
        {
            var error = Error.Validation(
                "INVALID_GROUP_TYPE",
                "Devices can only be manually added to static groups. Use tag queries for dynamic groups.");
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        // Verify device exists and belongs to tenant
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        if (device.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_ACCESS_DENIED",
                "You do not have permission to add this device.");
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        // Check if membership already exists
        var existingMembership = await _membershipRepository.GetByGroupAndDeviceAsync(
            deviceGroupId, deviceId, cancellationToken);

        if (existingMembership is not null)
        {
            var error = Error.Conflict(
                "MEMBERSHIP_ALREADY_EXISTS",
                $"Device {command.DeviceId} is already a member of group {command.DeviceGroupId}.");
            return Result.Failure<AddDeviceToGroupResponse>(error);
        }

        // Create new static membership
        // TODO: Get actual user ID from authentication context
        var addedBy = "user";
        var membership = DeviceGroupMembership.CreateStatic(
            DeviceGroupMembershipId.New(),
            deviceGroupId,
            deviceId,
            addedBy,
            DateTimeOffset.UtcNow);

        await _membershipRepository.AddAsync(membership, cancellationToken);
        await _membershipRepository.SaveChangesAsync(cancellationToken);

        return Result<AddDeviceToGroupResponse>.Success(new AddDeviceToGroupResponse(
            MembershipId: membership.Id.Value,
            DeviceGroupId: membership.GroupId.Value,
            DeviceId: membership.DeviceId.Value,
            AddedAt: membership.AddedAt));
    }
}
