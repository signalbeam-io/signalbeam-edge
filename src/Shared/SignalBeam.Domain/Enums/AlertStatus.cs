namespace SignalBeam.Domain.Enums;

/// <summary>
/// Alert lifecycle status.
/// </summary>
public enum AlertStatus
{
    /// <summary>
    /// Alert is active and requires attention.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Alert has been acknowledged by an operator.
    /// </summary>
    Acknowledged = 1,

    /// <summary>
    /// Alert condition has been resolved.
    /// </summary>
    Resolved = 2
}
