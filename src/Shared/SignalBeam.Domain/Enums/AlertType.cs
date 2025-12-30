namespace SignalBeam.Domain.Enums;

/// <summary>
/// Types of alerts that can be raised in the system.
/// </summary>
public enum AlertType
{
    /// <summary>
    /// Device has not sent heartbeat within threshold.
    /// </summary>
    DeviceOffline = 0,

    /// <summary>
    /// Device health score has fallen below acceptable threshold.
    /// </summary>
    DeviceUnhealthy = 1,

    /// <summary>
    /// API error rate has exceeded acceptable threshold.
    /// </summary>
    HighErrorRate = 2,

    /// <summary>
    /// Database connection or query failures detected.
    /// </summary>
    DatabaseFailure = 3,

    /// <summary>
    /// NATS message broker failures detected.
    /// </summary>
    MessageBrokerFailure = 4,

    /// <summary>
    /// Rollout has failed or exceeded failure threshold.
    /// </summary>
    RolloutFailure = 5,

    /// <summary>
    /// Device reconciliation is consistently failing.
    /// </summary>
    ReconciliationFailure = 6,

    /// <summary>
    /// Device resource utilization is critically high.
    /// </summary>
    HighResourceUtilization = 7
}
