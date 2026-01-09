namespace SignalBeam.Domain.Enums;

/// <summary>
/// Subscription status.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription is active and valid.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Subscription has been cancelled and ended.
    /// </summary>
    Cancelled = 1,

    /// <summary>
    /// Subscription has expired (if time-limited).
    /// </summary>
    Expired = 2
}
