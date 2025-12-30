using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

/// <summary>
/// Alert rule that detects devices experiencing high error rates.
/// Monitors reconciliation failures, container errors, and other device errors
/// within a configurable time window.
/// </summary>
public class HighErrorRateRule : IAlertRule
{
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<HighErrorRateRule> _logger;
    private readonly AlertingOptions _options;

    public string RuleId => "high_error_rate";
    public AlertType AlertType => AlertType.HighErrorRate;
    public bool IsEnabled => _options.Rules.HighErrorRate.Enabled;

    public HighErrorRateRule(
        IDeviceHeartbeatRepository heartbeatRepository,
        IAlertRepository alertRepository,
        ILogger<HighErrorRateRule> logger,
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
            _logger.LogDebug("HighErrorRateRule is disabled");
            return Array.Empty<Alert>();
        }

        var config = _options.Rules.HighErrorRate;
        var thresholdPercent = config.ThresholdPercent;
        var window = TimeSpan.FromMinutes(config.WindowMinutes);

        _logger.LogDebug(
            "Evaluating HighErrorRateRule with threshold: {Threshold}% over {Window} minutes",
            thresholdPercent,
            config.WindowMinutes);

        try
        {
            var alerts = new List<Alert>();

            // Get all active devices in the time window
            var since = DateTimeOffset.UtcNow - window;
            var activeDeviceIds = await _heartbeatRepository.GetActiveDeviceIdsAsync(since, cancellationToken);

            if (!activeDeviceIds.Any())
            {
                _logger.LogDebug("No active devices found in window");
                return Array.Empty<Alert>();
            }

            _logger.LogDebug("Checking error rates for {Count} active devices", activeDeviceIds.Count);

            foreach (var deviceId in activeDeviceIds)
            {
                // Get heartbeats in the time window
                var heartbeats = await _heartbeatRepository.GetByDeviceIdAndTimeRangeAsync(
                    deviceId,
                    since,
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                if (!heartbeats.Any())
                {
                    continue;
                }

                // Count heartbeats with "Error" or "Failed" status
                // Note: This assumes heartbeat.Status contains error indicators
                var errorCount = heartbeats.Count(h =>
                    h.Status != null &&
                    (h.Status.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                     h.Status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                     h.Status.Contains("Failure", StringComparison.OrdinalIgnoreCase)));

                var totalCount = heartbeats.Count;
                var errorRate = totalCount > 0 ? (errorCount * 100.0 / totalCount) : 0;

                if (errorRate < thresholdPercent)
                {
                    continue; // Error rate below threshold
                }

                // Check if alert already exists and is active
                var existingAlert = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                    deviceId,
                    AlertType.HighErrorRate,
                    cancellationToken);

                if (existingAlert != null)
                {
                    _logger.LogDebug(
                        "Alert already exists for device {DeviceId}, skipping",
                        deviceId);
                    continue;
                }

                // Create alert
                var description = $"Device is experiencing a high error rate of {errorRate:F1}% " +
                                  $"({errorCount} errors out of {totalCount} operations) " +
                                  $"over the last {config.WindowMinutes} minutes. " +
                                  $"Threshold is {thresholdPercent}%.";

                var alert = Alert.Create(
                    new TenantId(Guid.Empty), // TODO: Get tenant ID from device lookup
                    AlertSeverity.Warning,
                    AlertType.HighErrorRate,
                    $"Device {deviceId.Value} has high error rate",
                    description,
                    deviceId,
                    null);

                alerts.Add(alert);

                _logger.LogWarning(
                    "Created Warning alert for device {DeviceId} with error rate {ErrorRate:F1}% " +
                    "({ErrorCount}/{TotalCount})",
                    deviceId,
                    errorRate,
                    errorCount,
                    totalCount);
            }

            if (alerts.Any())
            {
                _logger.LogInformation(
                    "Found {Count} devices with high error rates",
                    alerts.Count);
            }

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating HighErrorRateRule");
            return Array.Empty<Alert>();
        }
    }
}
