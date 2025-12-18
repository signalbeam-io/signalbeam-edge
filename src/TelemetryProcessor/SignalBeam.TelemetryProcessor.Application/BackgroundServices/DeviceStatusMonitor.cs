using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Enums;
using SignalBeam.TelemetryProcessor.Application.Commands;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service that monitors device heartbeats and marks stale devices as offline.
/// Runs periodically to check for devices that haven't sent heartbeats within the threshold.
/// </summary>
public class DeviceStatusMonitor : BackgroundService
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly UpdateDeviceStatusHandler _updateStatusHandler;
    private readonly ILogger<DeviceStatusMonitor> _logger;
    private readonly DeviceStatusMonitorOptions _options;

    public DeviceStatusMonitor(
        IDeviceHeartbeatRepository heartbeatRepository,
        UpdateDeviceStatusHandler updateStatusHandler,
        ILogger<DeviceStatusMonitor> logger,
        IOptions<DeviceStatusMonitorOptions> options)
    {
        _heartbeatRepository = heartbeatRepository;
        _updateStatusHandler = updateStatusHandler;
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

        var staleDevices = await _heartbeatRepository.GetStaleDevicesAsync(
            _options.HeartbeatThreshold,
            cancellationToken);

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
                var command = new UpdateDeviceStatusCommand(
                    deviceId.Value,
                    DeviceStatus.Offline,
                    DateTimeOffset.UtcNow);

                var result = await _updateStatusHandler.Handle(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Marked device {DeviceId} as offline due to stale heartbeat",
                        deviceId.Value);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to mark device {DeviceId} as offline: {ErrorCode} - {ErrorMessage}",
                        deviceId.Value,
                        result.Error?.Code,
                        result.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error marking device {DeviceId} as offline",
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
