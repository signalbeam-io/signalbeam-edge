using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

/// <summary>
/// Command to record a heartbeat from a device.
/// </summary>
public record RecordHeartbeatCommand(
    Guid DeviceId,
    DateTimeOffset Timestamp);

/// <summary>
/// Response after recording a heartbeat.
/// </summary>
public record RecordHeartbeatResponse(
    Guid DeviceId,
    string Status,
    DateTimeOffset LastSeenAt);

/// <summary>
/// Handler for RecordHeartbeatCommand.
/// </summary>
public class RecordHeartbeatHandler
{
    private readonly IDeviceRepository _deviceRepository;

    public RecordHeartbeatHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<RecordHeartbeatResponse>> Handle(
        RecordHeartbeatCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<RecordHeartbeatResponse>(error);
        }

        // Record heartbeat (this will raise domain events if status changes)
        device.RecordHeartbeat(command.Timestamp);

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<RecordHeartbeatResponse>.Success(new RecordHeartbeatResponse(
            device.Id.Value,
            device.Status.ToString(),
            device.LastSeenAt!.Value));
    }
}
