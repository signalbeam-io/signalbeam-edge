using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

// Alerts when CPU, memory or disk stays at or above the threshold for every sample in the window.
// A single spike doesn't alert; it has to be sustained.
public class HighResourceUtilizationRule : IAlertRule
{
    private const int MinSamples = 2;

    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly IDeviceMetricsRepository _metricsRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<HighResourceUtilizationRule> _logger;
    private readonly AlertingOptions _options;
    private readonly IDeviceTenantResolver _tenantResolver;
    private readonly TimeProvider _timeProvider;

    public string RuleId => "high_resource_utilization";
    public AlertType AlertType => AlertType.HighResourceUtilization;
    public bool IsEnabled => _options.Rules.HighResourceUtilization.Enabled;

    public HighResourceUtilizationRule(
        IDeviceHeartbeatRepository heartbeatRepository,
        IDeviceMetricsRepository metricsRepository,
        IAlertRepository alertRepository,
        ILogger<HighResourceUtilizationRule> logger,
        IOptions<AlertingOptions> options,
        IDeviceTenantResolver tenantResolver,
        TimeProvider? timeProvider = null)
    {
        _heartbeatRepository = heartbeatRepository;
        _metricsRepository = metricsRepository;
        _alertRepository = alertRepository;
        _logger = logger;
        _options = options.Value;
        _tenantResolver = tenantResolver;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Array.Empty<Alert>();
        }

        var config = _options.Rules.HighResourceUtilization;
        var now = _timeProvider.GetUtcNow();
        var since = now.AddMinutes(-config.WindowMinutes);
        var severity = Enum.TryParse<AlertSeverity>(config.Severity, ignoreCase: true, out var parsed)
            ? parsed
            : AlertSeverity.Warning;

        try
        {
            var alerts = new List<Alert>();
            var deviceIds = await _heartbeatRepository.GetActiveDeviceIdsAsync(since, cancellationToken);

            foreach (var deviceId in deviceIds)
            {
                var metrics = await _metricsRepository.GetByDeviceIdAndTimeRangeAsync(deviceId, since, now, cancellationToken);
                var saturated = SaturatedResources(metrics, config.ThresholdPercent);
                if (saturated.Count == 0)
                {
                    continue;
                }

                var existing = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                    deviceId, AlertType.HighResourceUtilization, cancellationToken);
                if (existing is not null)
                {
                    continue;
                }

                var latest = metrics.MaxBy(m => m.Timestamp)!;
                var tenantId = await _tenantResolver.ResolveAsync(deviceId, cancellationToken);
                if (tenantId is null)
                {
                    _logger.LogWarning("Skipping {RuleId} alert for device {DeviceId}: tenant unknown", RuleId, deviceId);
                    continue;
                }

                alerts.Add(Alert.Create(
                    tenantId.Value,
                    severity,
                    AlertType.HighResourceUtilization,
                    $"Device {deviceId.Value} is running out of {string.Join(" and ", saturated)}",
                    $"{string.Join(", ", saturated)} at or above {config.ThresholdPercent}% for {config.WindowMinutes} minutes "
                        + $"({metrics.Count} samples). Latest: CPU {latest.CpuUsage:F0}%, memory {latest.MemoryUsage:F0}%, "
                        + $"disk {latest.DiskUsage:F0}%.",
                    deviceId));

                _logger.LogWarning("High resource utilization on device {DeviceId}: {Resources}", deviceId, saturated);
            }

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating HighResourceUtilizationRule");
            return Array.Empty<Alert>();
        }
    }

    private static IReadOnlyList<string> SaturatedResources(IReadOnlyCollection<DeviceMetrics> metrics, double thresholdPercent)
    {
        if (metrics.Count < MinSamples)
        {
            return [];
        }

        var saturated = new List<string>();
        if (metrics.All(m => m.CpuUsage >= thresholdPercent))
        {
            saturated.Add("CPU");
        }

        if (metrics.All(m => m.MemoryUsage >= thresholdPercent))
        {
            saturated.Add("memory");
        }

        if (metrics.All(m => m.DiskUsage >= thresholdPercent))
        {
            saturated.Add("disk");
        }

        return saturated;
    }
}
