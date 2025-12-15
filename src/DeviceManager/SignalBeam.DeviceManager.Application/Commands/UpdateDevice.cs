using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to update device metadata.
/// </summary>
public record UpdateDeviceCommand(
    Guid DeviceId,
    string? Name = null,
    string? Metadata = null);

/// <summary>
/// Response after updating a device.
/// </summary>
public record UpdateDeviceResponse(
    Guid DeviceId,
    string Name,
    string? Metadata,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Handler for UpdateDeviceCommand.
/// </summary>
public class UpdateDeviceHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public UpdateDeviceHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<UpdateDeviceResponse>> Handle(
        UpdateDeviceCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<UpdateDeviceResponse>(error);
        }

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            device.UpdateName(command.Name);
        }

        // Update metadata if provided
        if (command.Metadata is not null)
        {
            device.UpdateMetadata(command.Metadata);
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<UpdateDeviceResponse>.Success(new UpdateDeviceResponse(
            device.Id.Value,
            device.Name,
            device.Metadata,
            DateTimeOffset.UtcNow));
    }
}
