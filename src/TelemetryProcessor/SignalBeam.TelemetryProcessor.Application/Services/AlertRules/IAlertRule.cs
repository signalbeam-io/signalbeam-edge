using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;

namespace SignalBeam.TelemetryProcessor.Application.Services.AlertRules;

/// <summary>
/// Interface for alert rule evaluators.
/// Each rule checks for a specific condition and creates alerts when the condition is met.
/// </summary>
public interface IAlertRule
{
    /// <summary>
    /// Unique identifier for this alert rule.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// The type of alert this rule generates.
    /// </summary>
    AlertType AlertType { get; }

    /// <summary>
    /// Whether this rule is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the rule and returns alerts that should be created.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of alerts to be created (empty if no alerts needed).</returns>
    Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken cancellationToken = default);
}
