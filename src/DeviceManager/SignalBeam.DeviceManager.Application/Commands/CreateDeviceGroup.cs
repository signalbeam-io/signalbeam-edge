using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to create a new device group.
/// </summary>
public record CreateDeviceGroupCommand(
    Guid TenantId,
    string Name,
    string? Description = null);

/// <summary>
/// Response after creating a device group.
/// </summary>
public record CreateDeviceGroupResponse(
    Guid DeviceGroupId,
    string Name,
    string? Description,
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

        // Check if group with same name already exists for this tenant
        var exists = await _groupRepository.ExistsByNameAsync(tenantId, command.Name, cancellationToken);
        if (exists)
        {
            var error = Error.Conflict(
                "DEVICE_GROUP_NAME_EXISTS",
                $"A device group with the name '{command.Name}' already exists for this tenant.");
            return Result.Failure<CreateDeviceGroupResponse>(error);
        }

        // Create new device group
        var deviceGroupId = new DeviceGroupId(Guid.NewGuid());
        var deviceGroup = DeviceGroup.Create(
            deviceGroupId,
            tenantId,
            command.Name,
            command.Description,
            DateTimeOffset.UtcNow);

        // Save to repository
        await _groupRepository.AddAsync(deviceGroup, cancellationToken);
        await _groupRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<CreateDeviceGroupResponse>.Success(new CreateDeviceGroupResponse(
            DeviceGroupId: deviceGroup.Id.Value,
            Name: deviceGroup.Name,
            Description: deviceGroup.Description,
            CreatedAt: deviceGroup.CreatedAt));
    }
}
