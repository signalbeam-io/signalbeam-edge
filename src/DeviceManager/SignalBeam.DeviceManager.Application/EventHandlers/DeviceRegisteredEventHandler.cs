using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Events;

namespace SignalBeam.DeviceManager.Application.EventHandlers;

/// <summary>
/// Handler for DeviceRegisteredEvent.
/// Performs side effects when a device is registered (e.g., logging, notifications, activity log).
/// </summary>
public class DeviceRegisteredEventHandler
{
    private readonly ILogger<DeviceRegisteredEventHandler> _logger;
    private readonly IDeviceActivityLogRepository _activityLogRepository;

    public DeviceRegisteredEventHandler(
        ILogger<DeviceRegisteredEventHandler> logger,
        IDeviceActivityLogRepository activityLogRepository)
    {
        _logger = logger;
        _activityLogRepository = activityLogRepository;
    }

    public async Task Handle(DeviceRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Device registered: {DeviceId} for tenant {TenantId} with name {DeviceName}",
            domainEvent.DeviceId,
            domainEvent.TenantId,
            domainEvent.DeviceName);

        // Add activity log entry
        var activityLog = DeviceActivityLog.Create(
            deviceId: domainEvent.DeviceId,
            timestamp: domainEvent.RegisteredAt,
            activityType: "DeviceRegistered",
            description: $"Device '{domainEvent.DeviceName}' was registered",
            severity: "Info");

        await _activityLogRepository.AddAsync(activityLog, cancellationToken);
        await _activityLogRepository.SaveChangesAsync(cancellationToken);

        // TODO: Send notification/webhook
        // TODO: Publish integration event for other services
    }
}
