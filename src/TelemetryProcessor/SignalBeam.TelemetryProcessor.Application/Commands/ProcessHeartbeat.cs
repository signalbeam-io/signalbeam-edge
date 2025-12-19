using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Messaging;
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
    string Status);

/// <summary>
/// Event published when a device heartbeat is received and processed.
/// DeviceManager subscribes to this to update device status.
/// </summary>
public record DeviceHeartbeatReceivedEvent(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string Status,
    string? IpAddress);

/// <summary>
/// Handler for ProcessHeartbeatCommand.
/// Stores heartbeat in TimescaleDB and publishes event for DeviceManager.
/// </summary>
public class ProcessHeartbeatHandler
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly IMessagePublisher _messagePublisher;

    public ProcessHeartbeatHandler(
        IDeviceHeartbeatRepository heartbeatRepository,
        IMessagePublisher messagePublisher)
    {
        _heartbeatRepository = heartbeatRepository;
        _messagePublisher = messagePublisher;
    }

    public async Task<Result<ProcessHeartbeatResponse>> Handle(
        ProcessHeartbeatCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(command.DeviceId);

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

        // Publish event for DeviceManager to update device status
        var @event = new DeviceHeartbeatReceivedEvent(
            command.DeviceId,
            command.Timestamp,
            command.Status,
            command.IpAddress);

        await _messagePublisher.PublishAsync(
            "signalbeam.devices.events.heartbeat_received",
            @event,
            cancellationToken);

        return Result<ProcessHeartbeatResponse>.Success(new ProcessHeartbeatResponse(
            heartbeat.Id,
            deviceId.Value,
            heartbeat.Timestamp,
            command.Status));
    }
}
