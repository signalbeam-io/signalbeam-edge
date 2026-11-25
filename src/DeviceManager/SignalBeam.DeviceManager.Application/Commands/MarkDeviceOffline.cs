using SignalBeam.DeviceManager.Application.EventHandlers;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Commands;

// From TelemetryProcessor's DeviceStatusMonitor (signalbeam.devices.events.stale_detected):
// no heartbeat over NATS since LastHeartbeatTime.
public record MarkDeviceOfflineCommand(
    Guid DeviceId,
    DateTimeOffset LastHeartbeatTime,
    DateTimeOffset DetectedAt);

public record MarkDeviceOfflineResponse(Guid DeviceId, bool MarkedOffline, string Status);

public class MarkDeviceOfflineHandler
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly DeviceOfflineEventHandler _offlineEventHandler;

    public MarkDeviceOfflineHandler(IDeviceRepository deviceRepository, DeviceOfflineEventHandler offlineEventHandler)
    {
        _deviceRepository = deviceRepository;
        _offlineEventHandler = offlineEventHandler;
    }

    public async Task<Result<MarkDeviceOfflineResponse>> Handle(
        MarkDeviceOfflineCommand command,
        CancellationToken cancellationToken)
    {
        var device = await _deviceRepository.GetByIdAsync(new DeviceId(command.DeviceId), cancellationToken);
        if (device is null)
        {
            return Result.Failure<MarkDeviceOfflineResponse>(
                Error.NotFound("DEVICE_NOT_FOUND", $"Device with ID {command.DeviceId} was not found."));
        }

        // The stale check only sees NATS heartbeats. A newer HTTP heartbeat means the device is up,
        // so don't flip it offline. The monitor re-publishes every cycle, so repeats are no-ops too.
        if (device.LastSeenAt > command.LastHeartbeatTime)
        {
            return Result<MarkDeviceOfflineResponse>.Success(
                new MarkDeviceOfflineResponse(device.Id.Value, false, device.Status.ToString()));
        }

        device.ClearDomainEvents();
        device.MarkAsOffline(command.DetectedAt);
        var offlineEvent = device.DomainEvents.OfType<DeviceOfflineEvent>().SingleOrDefault();

        if (offlineEvent is null)
        {
            return Result<MarkDeviceOfflineResponse>.Success(
                new MarkDeviceOfflineResponse(device.Id.Value, false, device.Status.ToString()));
        }

        await _deviceRepository.SaveChangesAsync(cancellationToken);

        // DeviceManager has no domain event dispatcher; run the existing handler (activity log) directly.
        await _offlineEventHandler.Handle(offlineEvent, cancellationToken);
        device.ClearDomainEvents();

        return Result<MarkDeviceOfflineResponse>.Success(
            new MarkDeviceOfflineResponse(device.Id.Value, true, device.Status.ToString()));
    }
}
