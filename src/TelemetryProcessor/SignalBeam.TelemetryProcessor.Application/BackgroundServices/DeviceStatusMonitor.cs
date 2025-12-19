using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Shared.Infrastructure.Messaging;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Event published when a device is detected as stale (no recent heartbeats).
/// DeviceManager subscribes to this to mark devices as offline.
/// </summary>
public record DeviceStaleDetectedEvent(
    Guid DeviceId,
    DateTimeOffset LastHeartbeatTime,
    DateTimeOffset DetectedAt);

/// <summary>
/// Background service that monitors device heartbeats and detects stale devices.
/// Publishes events for DeviceManager to mark devices as offline.
/// Runs periodically to check for devices that haven't sent heartbeats within the threshold.
/// </summary>
public class DeviceStatusMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DeviceStatusMonitor> _logger;
    private readonly DeviceStatusMonitorOptions _options;

    public DeviceStatusMonitor(
        IServiceScopeFactory scopeFactory,
        IMessagePublisher messagePublisher,
        ILogger<DeviceStatusMonitor> logger,
        IOptions<DeviceStatusMonitorOptions> options)
    {
        _scopeFactory = scopeFactory;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Device Status Monitor started. Check interval: {Interval}, Heartbeat threshold: {Threshold}",
            _options.CheckInterval,
            _options.HeartbeatThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorDeviceStatusAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Device Status Monitor");
            }

            await Task.Delay(_options.CheckInterval, stoppingToken);
        }
    }

    private async Task MonitorDeviceStatusAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking for stale devices...");

        IReadOnlyCollection<Domain.ValueObjects.DeviceId> staleDevices;

        // Create scope to resolve scoped repository
        using (var scope = _scopeFactory.CreateScope())
        {
            var heartbeatRepository = scope.ServiceProvider.GetRequiredService<IDeviceHeartbeatRepository>();
            staleDevices = await heartbeatRepository.GetStaleDevicesAsync(
                _options.HeartbeatThreshold,
                cancellationToken);
        }

        if (staleDevices.Count == 0)
        {
            _logger.LogDebug("No stale devices found");
            return;
        }

        _logger.LogInformation(
            "Found {Count} stale devices that need to be marked as offline",
            staleDevices.Count);

        foreach (var deviceId in staleDevices)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var lastHeartbeatTime = now - _options.HeartbeatThreshold;

                var @event = new DeviceStaleDetectedEvent(
                    deviceId.Value,
                    lastHeartbeatTime,
                    now);

                // Publish event for DeviceManager to mark device as offline
                await _messagePublisher.PublishAsync(
                    "signalbeam.devices.events.stale_detected",
                    @event,
                    cancellationToken);

                _logger.LogInformation(
                    "Published stale device event for {DeviceId} (no heartbeat since {LastHeartbeat})",
                    deviceId.Value,
                    lastHeartbeatTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error publishing stale device event for {DeviceId}",
                    deviceId.Value);
            }
        }
    }
}

/// <summary>
/// Configuration options for DeviceStatusMonitor.
/// </summary>
public class DeviceStatusMonitorOptions
{
    /// <summary>
    /// How often to check for stale devices.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Heartbeat threshold - devices that haven't sent a heartbeat within this time are marked offline.
    /// Default: 2 minutes (should be at least 2x the expected heartbeat interval).
    /// </summary>
    public TimeSpan HeartbeatThreshold { get; set; } = TimeSpan.FromMinutes(2);
}
