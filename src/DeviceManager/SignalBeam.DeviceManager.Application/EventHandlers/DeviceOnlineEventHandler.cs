using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Events;

namespace SignalBeam.DeviceManager.Application.EventHandlers;

/// <summary>
/// Handler for DeviceOnlineEvent.
/// Performs actions when a device comes online (e.g., logging, clearing alerts).
/// </summary>
public class DeviceOnlineEventHandler
{
    private readonly ILogger<DeviceOnlineEventHandler> _logger;
    private readonly IDeviceActivityLogRepository _activityLogRepository;

    public DeviceOnlineEventHandler(
        ILogger<DeviceOnlineEventHandler> logger,
        IDeviceActivityLogRepository activityLogRepository)
    {
        _logger = logger;
        _activityLogRepository = activityLogRepository;
    }

    public async Task Handle(DeviceOnlineEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Device came online: {DeviceId} at {OnlineSince}",
            domainEvent.DeviceId,
            domainEvent.OnlineSince);

        // Add activity log entry
        var activityLog = DeviceActivityLog.Create(
            deviceId: domainEvent.DeviceId,
            timestamp: domainEvent.OnlineSince,
            activityType: "DeviceOnline",
            description: "Device came back online",
            severity: "Info");

        await _activityLogRepository.AddAsync(activityLog, cancellationToken);
        await _activityLogRepository.SaveChangesAsync(cancellationToken);

        // TODO: Clear any offline alerts
        // TODO: Publish integration event
    }
}
