using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to remove a tag from a device.
/// </summary>
public record RemoveDeviceTagCommand(
    Guid DeviceId,
    string Tag);

/// <summary>
/// Response after removing a tag.
/// </summary>
public record RemoveDeviceTagResponse(
    Guid DeviceId,
    IReadOnlyCollection<string> Tags);

/// <summary>
/// Handler for RemoveDeviceTagCommand.
/// </summary>
public class RemoveDeviceTagHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public RemoveDeviceTagHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<RemoveDeviceTagResponse>> Handle(
        RemoveDeviceTagCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<RemoveDeviceTagResponse>(error);
        }

        // Remove tag from device
        device.RemoveTag(command.Tag);

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<RemoveDeviceTagResponse>.Success(new RemoveDeviceTagResponse(
            device.Id.Value,
            device.Tags));
    }
}
