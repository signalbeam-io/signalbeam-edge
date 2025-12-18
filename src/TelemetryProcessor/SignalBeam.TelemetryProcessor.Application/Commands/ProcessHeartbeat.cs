using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Commands;

/// <summary>
/// Command to process a heartbeat from a device.
/// </summary>
public record ProcessHeartbeatCommand(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string Status,
    string? IpAddress = null,
    string? AdditionalData = null);

/// <summary>
/// Response after processing a heartbeat.
/// </summary>
public record ProcessHeartbeatResponse(
    Guid HeartbeatId,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string DeviceStatus);

/// <summary>
/// Handler for ProcessHeartbeatCommand.
/// Stores heartbeat in TimescaleDB and updates device status.
/// </summary>
public class ProcessHeartbeatHandler
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly IDeviceRepository _deviceRepository;

    public ProcessHeartbeatHandler(
        IDeviceHeartbeatRepository heartbeatRepository,
        IDeviceRepository deviceRepository)
    {
        _heartbeatRepository = heartbeatRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<Result<ProcessHeartbeatResponse>> Handle(
        ProcessHeartbeatCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);

        // Get device to update its status
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            var error = Error.NotFound(
                "DEVICE_NOT_FOUND",
                $"Device with ID {command.DeviceId} was not found.");
            return Result.Failure<ProcessHeartbeatResponse>(error);
        }

        // Create heartbeat record
        var heartbeat = DeviceHeartbeat.Create(
            deviceId,
            command.Timestamp,
            command.Status,
            command.IpAddress,
            command.AdditionalData);

        // Store heartbeat in TimescaleDB
        await _heartbeatRepository.AddAsync(heartbeat, cancellationToken);
        await _heartbeatRepository.SaveChangesAsync(cancellationToken);

        // Update device status (this will raise domain events if status changes)
        device.RecordHeartbeat(command.Timestamp);
        await _deviceRepository.SaveChangesAsync(cancellationToken);

        return Result<ProcessHeartbeatResponse>.Success(new ProcessHeartbeatResponse(
            heartbeat.Id,
            device.Id.Value,
            heartbeat.Timestamp,
            device.Status.ToString()));
    }
}
