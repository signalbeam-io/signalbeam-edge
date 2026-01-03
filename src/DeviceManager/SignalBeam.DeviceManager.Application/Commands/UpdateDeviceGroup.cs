using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to update an existing device group.
/// </summary>
public record UpdateDeviceGroupCommand(
    Guid TenantId,
    Guid DeviceGroupId,
    string? Name = null,
    string? Description = null,
    string? TagQuery = null);

/// <summary>
/// Response after updating a device group.
/// </summary>
public record UpdateDeviceGroupResponse(
    Guid DeviceGroupId,
    string Name,
    string? Description,
    GroupType Type,
    string? TagQuery,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Handler for UpdateDeviceGroupCommand.
/// </summary>
public class UpdateDeviceGroupHandler
{
    private readonly IDeviceGroupRepository _groupRepository;

    public UpdateDeviceGroupHandler(IDeviceGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Result<UpdateDeviceGroupResponse>> Handle(
        UpdateDeviceGroupCommand command,
        CancellationToken cancellationToken)
    {
        var deviceGroupId = new DeviceGroupId(command.DeviceGroupId);
        var tenantId = new TenantId(command.TenantId);

        // Get existing group
        var deviceGroup = await _groupRepository.GetByIdAsync(deviceGroupId, cancellationToken);

        if (deviceGroup is null)
        {
            var error = Error.NotFound(
                "DEVICE_GROUP_NOT_FOUND",
                $"Device group with ID {command.DeviceGroupId} was not found.");
            return Result.Failure<UpdateDeviceGroupResponse>(error);
        }

        // Verify tenant owns this group
        if (deviceGroup.TenantId != tenantId)
        {
            var error = Error.Forbidden(
                "DEVICE_GROUP_ACCESS_DENIED",
                "You do not have permission to update this device group.");
            return Result.Failure<UpdateDeviceGroupResponse>(error);
        }

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(command.Name) && command.Name != deviceGroup.Name)
        {
            // Check if new name already exists for this tenant
            var nameExists = await _groupRepository.ExistsByNameAsync(tenantId, command.Name, cancellationToken);
            if (nameExists)
            {
                var error = Error.Conflict(
                    "DEVICE_GROUP_NAME_EXISTS",
                    $"A device group with the name '{command.Name}' already exists for this tenant.");
                return Result.Failure<UpdateDeviceGroupResponse>(error);
            }

            deviceGroup.UpdateName(command.Name);
        }

        // Update description if provided
        if (command.Description is not null)
        {
            deviceGroup.UpdateDescription(command.Description);
        }

        // Update tag query if provided (only for dynamic groups)
        if (command.TagQuery is not null)
        {
            if (deviceGroup.Type != GroupType.Dynamic)
            {
                var error = Error.Validation(
                    "INVALID_GROUP_TYPE",
                    "Tag query can only be set for dynamic groups.");
                return Result.Failure<UpdateDeviceGroupResponse>(error);
            }

            try
            {
                deviceGroup.UpdateTagQuery(command.TagQuery);
            }
            catch (ArgumentException ex)
            {
                var error = Error.Validation(
                    "INVALID_TAG_QUERY",
                    ex.Message);
                return Result.Failure<UpdateDeviceGroupResponse>(error);
            }
        }

        await _groupRepository.SaveChangesAsync(cancellationToken);

        return Result<UpdateDeviceGroupResponse>.Success(new UpdateDeviceGroupResponse(
            DeviceGroupId: deviceGroup.Id.Value,
            Name: deviceGroup.Name,
            Description: deviceGroup.Description,
            Type: deviceGroup.Type,
            TagQuery: deviceGroup.TagQuery,
            UpdatedAt: DateTimeOffset.UtcNow));
    }
}
