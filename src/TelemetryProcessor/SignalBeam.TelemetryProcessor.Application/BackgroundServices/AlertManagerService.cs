using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

namespace SignalBeam.TelemetryProcessor.Application.BackgroundServices;

/// <summary>
/// Background service that periodically evaluates alert rules and creates alerts.
/// Executes all registered IAlertRule implementations at a configurable interval.
/// </summary>
public class AlertManagerService : BackgroundService
{
    private readonly IEnumerable<IAlertRule> _alertRules;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertManagerService> _logger;
    private readonly AlertManagerOptions _options;

    public AlertManagerService(
        IEnumerable<IAlertRule> alertRules,
        IAlertRepository alertRepository,
        ILogger<AlertManagerService> logger,
        IOptions<AlertManagerOptions> options)
    {
        _alertRules = alertRules;
        _alertRepository = alertRepository;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("AlertManagerService is disabled");
            return;
        }

        _logger.LogInformation(
            "AlertManagerService started. Check interval: {Interval}",
            _options.CheckInterval);

        using var timer = new PeriodicTimer(_options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateRulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert rule evaluation cycle");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }

        _logger.LogInformation("AlertManagerService stopped");
    }

    private async Task EvaluateRulesAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Starting alert rule evaluation cycle");

        var enabledRules = _alertRules.Where(r => r.IsEnabled).ToList();

        if (!enabledRules.Any())
        {
            _logger.LogDebug("No enabled alert rules found");
            return;
        }

        _logger.LogDebug("Evaluating {Count} enabled alert rules", enabledRules.Count);

        var totalAlerts = 0;

        foreach (var rule in enabledRules)
        {
            try
            {
                _logger.LogDebug("Evaluating rule: {RuleId}", rule.RuleId);

                var alerts = await rule.EvaluateAsync(cancellationToken);

                if (alerts.Any())
                {
                    _logger.LogInformation(
                        "Rule {RuleId} generated {Count} alerts",
                        rule.RuleId,
                        alerts.Count);

                    // Save alerts to database
                    // Note: AddAsync already calls SaveChangesAsync internally
                    foreach (var alert in alerts)
                    {
                        await _alertRepository.AddAsync(alert, cancellationToken);
                        totalAlerts++;

                        _logger.LogInformation(
                            "Created alert: {AlertType} - {Title} (Device: {DeviceId}, Severity: {Severity})",
                            alert.Type,
                            alert.Title,
                            alert.DeviceId,
                            alert.Severity);
                    }
                }
                else
                {
                    _logger.LogDebug("Rule {RuleId} generated no alerts", rule.RuleId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error evaluating alert rule {RuleId}",
                    rule.RuleId);
                // Continue with other rules even if one fails
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        if (totalAlerts > 0)
        {
            _logger.LogInformation(
                "Alert evaluation cycle completed. Created {TotalAlerts} alerts in {Duration}ms",
                totalAlerts,
                duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogDebug(
                "Alert evaluation cycle completed. No alerts created. Duration: {Duration}ms",
                duration.TotalMilliseconds);
        }
    }
}

/// <summary>
/// Configuration options for the AlertManagerService.
/// </summary>
public class AlertManagerOptions
{
    public const string SectionName = "AlertManager";

    /// <summary>
    /// Whether the alert manager service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to evaluate alert rules.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}
