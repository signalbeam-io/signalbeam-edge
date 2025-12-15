using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to add a tag to a device.
/// </summary>
public record AddDeviceTagCommand(
    Guid DeviceId,
    string Tag);

/// <summary>
/// Response after adding a tag.
/// </summary>
public record AddDeviceTagResponse(
    Guid DeviceId,
    IReadOnlyCollection<string> Tags);

/// <summary>
/// Handler for AddDeviceTagCommand.
/// </summary>
public class AddDeviceTagHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public AddDeviceTagHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<AddDeviceTagResponse>> Handle(
        AddDeviceTagCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<AddDeviceTagResponse>(error);
        }

        // Add tag to device
        device.AddTag(command.Tag);

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<AddDeviceTagResponse>.Success(new AddDeviceTagResponse(
            device.Id.Value,
            device.Tags));
    }
}
