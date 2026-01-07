namespace SignalBeam.Domain.Enums;

/// <summary>
/// Subscription tier for tenant quotas and features.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier with limited quotas (5 devices, 7 days retention).
    /// </summary>
    Free = 0,

    /// <summary>
    /// Paid tier with unlimited quotas (unlimited devices, 90 days retention).
    /// </summary>
    Paid = 1
}

/// <summary>
/// Extension methods for SubscriptionTier quota calculations.
/// </summary>
public static class SubscriptionTierExtensions
{
    /// <summary>
    /// Gets the maximum number of devices allowed for the subscription tier.
    /// </summary>
    public static int GetMaxDevices(this SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => 5,
        SubscriptionTier.Paid => int.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Invalid subscription tier")
    };

    /// <summary>
    /// Gets the data retention period in days for the subscription tier.
    /// </summary>
    public static int GetDataRetentionDays(this SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => 7,
        SubscriptionTier.Paid => 90,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Invalid subscription tier")
    };
}
