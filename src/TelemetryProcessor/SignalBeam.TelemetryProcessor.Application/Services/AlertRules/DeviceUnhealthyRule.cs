using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

/// <summary>
/// Alert rule that detects devices with critically low health scores.
/// Creates alerts when device health score falls below the configured threshold.
/// Health scores are calculated by HealthMonitorService and stored in TimescaleDB.
/// </summary>
public class DeviceUnhealthyRule : IAlertRule
{
    private readonly IDeviceHealthScoreRepository _healthScoreRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<DeviceUnhealthyRule> _logger;
    private readonly AlertingOptions _options;

    public string RuleId => "device_unhealthy";
    public AlertType AlertType => AlertType.DeviceUnhealthy;
    public bool IsEnabled => _options.Rules.DeviceUnhealthy.Enabled;

    public DeviceUnhealthyRule(
        IDeviceHealthScoreRepository healthScoreRepository,
        IAlertRepository alertRepository,
        ILogger<DeviceUnhealthyRule> logger,
        IOptions<AlertingOptions> options)
    {
        _healthScoreRepository = healthScoreRepository;
        _alertRepository = alertRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("DeviceUnhealthyRule is disabled");
            return Array.Empty<Alert>();
        }

        var config = _options.Rules.DeviceUnhealthy;
        var threshold = config.ThresholdScore;

        _logger.LogDebug("Evaluating DeviceUnhealthyRule with threshold: {Threshold}", threshold);

        try
        {
            // Get devices with health scores below threshold (check last 10 minutes)
            var since = DateTimeOffset.UtcNow.AddMinutes(-10);
            var unhealthyDeviceIds = await _healthScoreRepository.GetUnhealthyDevicesAsync(
                threshold,
                since,
                cancellationToken);

            if (!unhealthyDeviceIds.Any())
            {
                _logger.LogDebug("No unhealthy devices detected");
                return Array.Empty<Alert>();
            }

            _logger.LogInformation("Found {Count} potentially unhealthy devices", unhealthyDeviceIds.Count);

            var alerts = new List<Alert>();

            foreach (var deviceId in unhealthyDeviceIds)
            {
                // Check if alert already exists and is active
                var existingAlert = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                    deviceId,
                    AlertType.DeviceUnhealthy,
                    cancellationToken);

                if (existingAlert != null)
                {
                    _logger.LogDebug("Alert already exists for device {DeviceId}, skipping", deviceId);
                    continue; // Alert already raised, avoid duplicates
                }

                // Get the latest health score to include details in alert
                var latestHealthScore = await _healthScoreRepository.GetLatestByDeviceIdAsync(deviceId, cancellationToken);

                if (latestHealthScore == null)
                {
                    _logger.LogWarning("No health score found for device {DeviceId}", deviceId);
                    continue;
                }

                // Create detailed description with health score breakdown
                var description = $"Device health score is {latestHealthScore.TotalScore}/100 (below threshold of {threshold}). " +
                                  $"Breakdown: Heartbeat {latestHealthScore.HeartbeatScore}/40, " +
                                  $"Reconciliation {latestHealthScore.ReconciliationScore}/30, " +
                                  $"Resources {latestHealthScore.ResourceScore}/30. " +
                                  $"Calculated at {latestHealthScore.Timestamp:yyyy-MM-dd HH:mm:ss} UTC.";

                // Create alert
                var alert = Alert.Create(
                    new TenantId(Guid.Empty), // TODO: Get tenant ID from device lookup
                    AlertSeverity.Critical,
                    AlertType.DeviceUnhealthy,
                    $"Device {deviceId.Value} is unhealthy",
                    description,
                    deviceId,
                    null);

                alerts.Add(alert);

                _logger.LogWarning(
                    "Created Critical alert for unhealthy device {DeviceId} (health score: {Score}/100)",
                    deviceId,
                    latestHealthScore.TotalScore);
            }

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating DeviceUnhealthyRule");
            return Array.Empty<Alert>();
        }
    }
}
