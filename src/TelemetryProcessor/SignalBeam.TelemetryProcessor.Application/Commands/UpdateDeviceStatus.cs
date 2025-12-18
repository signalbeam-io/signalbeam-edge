using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Commands;

/// <summary>
/// Command to update device online/offline status.
/// </summary>
public record UpdateDeviceStatusCommand(
    Guid DeviceId,
    DeviceStatus Status,
    DateTimeOffset Timestamp);

/// <summary>
/// Response after updating device status.
/// </summary>
public record UpdateDeviceStatusResponse(
    Guid DeviceId,
    string Status,
    DateTimeOffset Timestamp);

/// <summary>
/// Handler for UpdateDeviceStatusCommand.
/// Updates device status based on heartbeat monitoring.
/// </summary>
public class UpdateDeviceStatusHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public UpdateDeviceStatusHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<UpdateDeviceStatusResponse>> Handle(
        UpdateDeviceStatusCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<UpdateDeviceStatusResponse>(error);
        }

        // Update device status (this will raise domain events)
        if (command.Status == DeviceStatus.Offline)
        {
            device.MarkAsOffline(command.Timestamp);
        }
        else if (command.Status == DeviceStatus.Online)
        {
            device.RecordHeartbeat(command.Timestamp);
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<UpdateDeviceStatusResponse>.Success(new UpdateDeviceStatusResponse(
            device.Id.Value,
            device.Status.ToString(),
            command.Timestamp));
    }
}
