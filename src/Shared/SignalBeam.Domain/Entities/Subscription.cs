using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Subscription entity tracking tenant's subscription tier and usage.
/// </summary>
public class Subscription : Entity<Guid>
{
    /// <summary>
    /// Tenant this subscription belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Current subscription tier.
    /// </summary>
    public SubscriptionTier Tier { get; private set; }

    /// <summary>
    /// Subscription status (Active, Cancelled, Expired).
    /// </summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>
    /// Current device count for quota tracking.
    /// </summary>
    public int DeviceCount { get; private set; }

    /// <summary>
    /// Maximum devices allowed (cached from tier for performance).
    /// </summary>
    public int MaxDevices => Tier.GetMaxDevices();

    /// <summary>
    /// Data retention days (cached from tier for performance).
    /// </summary>
    public int DataRetentionDays => Tier.GetDataRetentionDays();

    /// <summary>
    /// When the subscription started (UTC).
    /// </summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// When the subscription ended (UTC), if cancelled or expired.
    /// </summary>
    public DateTimeOffset? EndedAt { get; private set; }

    // EF Core constructor
    private Subscription() : base()
    {
    }

    private Subscription(
        Guid id,
        TenantId tenantId,
        SubscriptionTier tier,
        DateTimeOffset startedAt) : base(id)
    {
        TenantId = tenantId;
        Tier = tier;
        Status = SubscriptionStatus.Active;
        DeviceCount = 0;
        StartedAt = startedAt;
    }

    /// <summary>
    /// Factory method to create a new subscription.
    /// </summary>
    public static Subscription Create(
        Guid id,
        TenantId tenantId,
        SubscriptionTier tier,
        DateTimeOffset startedAt)
    {
        return new Subscription(id, tenantId, tier, startedAt);
    }

    /// <summary>
    /// Changes the subscription tier.
    /// </summary>
    public void ChangeTier(SubscriptionTier newTier, DateTimeOffset changedAt)
    {
        Tier = newTier;
    }

    /// <summary>
    /// Increments the device count and checks quota.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when device quota is exceeded.</exception>
    public void IncrementDeviceCount()
    {
        if (DeviceCount >= MaxDevices)
        {
            throw new InvalidOperationException(
                $"Device quota exceeded. Current tier allows maximum {MaxDevices} devices. Please upgrade your subscription.");
        }

        DeviceCount++;
    }

    /// <summary>
    /// Decrements the device count when a device is removed.
    /// </summary>
    public void DecrementDeviceCount()
    {
        if (DeviceCount > 0)
        {
            DeviceCount--;
        }
    }

    /// <summary>
    /// Cancels the subscription.
    /// </summary>
    public void Cancel(DateTimeOffset cancelledAt)
    {
        Status = SubscriptionStatus.Cancelled;
        EndedAt = cancelledAt;
    }

    /// <summary>
    /// Marks the subscription as expired.
    /// </summary>
    public void Expire(DateTimeOffset expiredAt)
    {
        Status = SubscriptionStatus.Expired;
        EndedAt = expiredAt;
    }
}
