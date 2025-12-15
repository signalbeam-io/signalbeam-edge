using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

public record AssignDeviceToGroupCommand(
    Guid DeviceId,
    Guid? DeviceGroupId);

public record AssignDeviceToGroupResponse(
    Guid DeviceId,
    Guid? DeviceGroupId,
    DateTimeOffset AssignedAt);

/// <summary>
/// Handler for assigning a device to a device group.
/// </summary>
public class AssignDeviceToGroupHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public AssignDeviceToGroupHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<AssignDeviceToGroupResponse>> Handle(
        AssignDeviceToGroupCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound("DEVICE_NOT_FOUND", $"Device with ID {command.DeviceId} not found.");
            return Result.Failure<AssignDeviceToGroupResponse>(error);
        }

        if (command.DeviceGroupId.HasValue)
        {
            var groupId = new DeviceGroupId(command.DeviceGroupId.Value);
            device.AssignToGroup(groupId);
        }
        else
        {
            device.RemoveFromGroup();
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<AssignDeviceToGroupResponse>.Success(new AssignDeviceToGroupResponse(
            DeviceId: device.Id.Value,
            DeviceGroupId: device.DeviceGroupId?.Value,
            AssignedAt: DateTimeOffset.UtcNow));
    }
}
