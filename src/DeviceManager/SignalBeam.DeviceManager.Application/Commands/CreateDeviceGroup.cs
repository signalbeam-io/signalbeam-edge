using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to create a new device group.
/// </summary>
public record CreateDeviceGroupCommand(
    Guid TenantId,
    string Name,
    string? Description = null,
    GroupType Type = GroupType.Static,
    string? TagQuery = null);

/// <summary>
/// Response after creating a device group.
/// </summary>
public record CreateDeviceGroupResponse(
    Guid DeviceGroupId,
    string Name,
    string? Description,
    GroupType Type,
    string? TagQuery,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for CreateDeviceGroupCommand.
/// </summary>
public class CreateDeviceGroupHandler
{
    private readonly IDeviceGroupRepository _groupRepository;

    public CreateDeviceGroupHandler(IDeviceGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Result<CreateDeviceGroupResponse>> Handle(
        CreateDeviceGroupCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(command.TenantId);

        // Validate dynamic group must have tag query
        if (command.Type == GroupType.Dynamic && string.IsNullOrWhiteSpace(command.TagQuery))
        {
            var error = Error.Validation(
                "TAG_QUERY_REQUIRED",
                "Dynamic groups must have a tag query.");
            return Result.Failure<CreateDeviceGroupResponse>(error);
        }

        // Validate static group should not have tag query
        if (command.Type == GroupType.Static && !string.IsNullOrWhiteSpace(command.TagQuery))
        {
            var error = Error.Validation(
                "TAG_QUERY_NOT_ALLOWED",
                "Static groups cannot have a tag query.");
            return Result.Failure<CreateDeviceGroupResponse>(error);
        }

        // Check if group with same name already exists for this tenant
        var exists = await _groupRepository.ExistsByNameAsync(tenantId, command.Name, cancellationToken);
        if (exists)
        {
            var error = Error.Conflict(
                "DEVICE_GROUP_NAME_EXISTS",
                $"A device group with the name '{command.Name}' already exists for this tenant.");
            return Result.Failure<CreateDeviceGroupResponse>(error);
        }

        // Create device group based on type
        var deviceGroupId = new DeviceGroupId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        DeviceGroup deviceGroup = command.Type switch
        {
            GroupType.Static => DeviceGroup.CreateStatic(
                deviceGroupId,
                tenantId,
                command.Name,
                command.Description,
                now),

            GroupType.Dynamic => DeviceGroup.CreateDynamic(
                deviceGroupId,
                tenantId,
                command.Name,
                command.Description,
                command.TagQuery!,
                now),

            _ => throw new InvalidOperationException($"Unsupported group type: {command.Type}")
        };

        // Save to repository
        await _groupRepository.AddAsync(deviceGroup, cancellationToken);
        await _groupRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<CreateDeviceGroupResponse>.Success(new CreateDeviceGroupResponse(
            DeviceGroupId: deviceGroup.Id.Value,
            Name: deviceGroup.Name,
            Description: deviceGroup.Description,
            Type: deviceGroup.Type,
            TagQuery: deviceGroup.TagQuery,
            CreatedAt: deviceGroup.CreatedAt));
    }
}
