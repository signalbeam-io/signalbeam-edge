using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Events;

namespace SignalBeam.DeviceManager.Application.EventHandlers;

/// <summary>
/// Handler for DeviceOfflineEvent.
/// Performs actions when a device goes offline (e.g., logging, alerting).
/// </summary>
public class DeviceOfflineEventHandler
{
    private readonly ILogger<DeviceOfflineEventHandler> _logger;
    private readonly IDeviceActivityLogRepository _activityLogRepository;

    public DeviceOfflineEventHandler(
        ILogger<DeviceOfflineEventHandler> logger,
        IDeviceActivityLogRepository activityLogRepository)
    {
        _logger = logger;
        _activityLogRepository = activityLogRepository;
    }

    public async Task Handle(DeviceOfflineEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Device went offline: {DeviceId} at {OfflineSince}",
            domainEvent.DeviceId,
            domainEvent.OfflineSince);

        // Add activity log entry
        var activityLog = DeviceActivityLog.Create(
            deviceId: domainEvent.DeviceId,
            timestamp: domainEvent.OfflineSince,
            activityType: "DeviceOffline",
            description: "Device went offline",
            severity: "Warning");

        await _activityLogRepository.AddAsync(activityLog, cancellationToken);
        await _activityLogRepository.SaveChangesAsync(cancellationToken);

        // TODO: Create alert/notification if offline for extended period
        // TODO: Publish integration event
    }
}
