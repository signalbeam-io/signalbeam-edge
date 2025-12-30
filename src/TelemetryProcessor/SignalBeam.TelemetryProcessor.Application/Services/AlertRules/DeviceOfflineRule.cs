using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

/// <summary>
/// Alert rule that detects devices that have stopped sending heartbeats.
/// Creates alerts when devices haven't sent a heartbeat within the configured threshold.
/// Supports both warning (5 minutes) and critical (30 minutes) thresholds.
/// </summary>
public class DeviceOfflineRule : IAlertRule
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<DeviceOfflineRule> _logger;
    private readonly AlertingOptions _options;

    public string RuleId => "device_offline";
    public AlertType AlertType => AlertType.DeviceOffline;
    public bool IsEnabled => _options.Rules.DeviceOfflineWarning.Enabled;

    public DeviceOfflineRule(
        IDeviceHeartbeatRepository heartbeatRepository,
        IAlertRepository alertRepository,
        ILogger<DeviceOfflineRule> logger,
        IOptions<AlertingOptions> options)
    {
        _heartbeatRepository = heartbeatRepository;
        _alertRepository = alertRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("DeviceOfflineRule is disabled");
            return Array.Empty<Alert>();
        }

        var config = _options.Rules.DeviceOfflineWarning;
        var threshold = TimeSpan.FromMinutes(config.ThresholdMinutes);

        _logger.LogDebug("Evaluating DeviceOfflineRule with threshold: {Threshold}", threshold);

        try
        {
            // Get devices that haven't sent heartbeat within threshold
            var staleDeviceIds = await _heartbeatRepository.GetStaleDevicesAsync(threshold, cancellationToken);

            if (!staleDeviceIds.Any())
            {
                _logger.LogDebug("No offline devices detected");
                return Array.Empty<Alert>();
            }

            _logger.LogInformation("Found {Count} potentially offline devices", staleDeviceIds.Count);

            var alerts = new List<Alert>();

            foreach (var deviceId in staleDeviceIds)
            {
                // Check if alert already exists and is active
                var existingAlert = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                    deviceId,
                    AlertType.DeviceOffline,
                    cancellationToken);

                if (existingAlert != null)
                {
                    _logger.LogDebug("Alert already exists for device {DeviceId}, skipping", deviceId);
                    continue; // Alert already raised, avoid duplicates
                }

                // Get the last heartbeat to calculate duration
                var lastHeartbeat = await _heartbeatRepository.GetLatestByDeviceIdAsync(deviceId, cancellationToken);

                if (lastHeartbeat == null)
                {
                    _logger.LogWarning("No heartbeat found for device {DeviceId}", deviceId);
                    continue;
                }

                var duration = DateTimeOffset.UtcNow - lastHeartbeat.Timestamp;

                // Determine severity based on duration
                var severity = DetermineSeverity(duration);

                // Create alert
                var alert = Alert.Create(
                    new TenantId(Guid.Empty), // TODO: Get tenant ID from heartbeat or device lookup
                    severity,
                    AlertType.DeviceOffline,
                    $"Device {deviceId.Value} is offline",
                    $"No heartbeat received for {duration.TotalMinutes:F0} minutes (last seen: {lastHeartbeat.Timestamp:yyyy-MM-dd HH:mm:ss} UTC)",
                    deviceId,
                    null);

                alerts.Add(alert);

                _logger.LogWarning(
                    "Created {Severity} alert for offline device {DeviceId} (offline for {Duration})",
                    severity,
                    deviceId,
                    duration);
            }

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating DeviceOfflineRule");
            return Array.Empty<Alert>();
        }
    }

    /// <summary>
    /// Determines alert severity based on how long the device has been offline.
    /// </summary>
    private AlertSeverity DetermineSeverity(TimeSpan offlineDuration)
    {
        var criticalThreshold = _options.Rules.DeviceOfflineCritical.ThresholdMinutes;

        if (offlineDuration.TotalMinutes >= criticalThreshold)
        {
            return AlertSeverity.Critical;
        }

        return AlertSeverity.Warning;
    }
}
