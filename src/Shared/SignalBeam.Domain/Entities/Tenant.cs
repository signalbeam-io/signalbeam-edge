using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Tenant aggregate root representing a workspace with subscription-based quotas.
/// </summary>
public class Tenant : AggregateRoot<TenantId>
{
    /// <summary>
    /// Tenant display name (e.g., "Acme Corporation").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug for tenant identification (e.g., "acme-corp").
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Current subscription tier determining quotas and features.
    /// </summary>
    public SubscriptionTier SubscriptionTier { get; private set; }

    /// <summary>
    /// Account status (Active, Suspended, Deleted).
    /// </summary>
    public TenantStatus Status { get; private set; }

    /// <summary>
    /// Maximum number of devices allowed based on subscription tier.
    /// </summary>
    public int MaxDevices { get; private set; }

    /// <summary>
    /// Data retention period in days based on subscription tier.
    /// </summary>
    public int DataRetentionDays { get; private set; }

    /// <summary>
    /// When the tenant was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the subscription was last upgraded (UTC).
    /// </summary>
    public DateTimeOffset? UpgradedAt { get; private set; }

    // EF Core constructor
    private Tenant() : base()
    {
    }

    private Tenant(
        TenantId id,
        string name,
        string slug,
        SubscriptionTier subscriptionTier,
        DateTimeOffset createdAt) : base(id)
    {
        Name = name;
        Slug = slug;
        SubscriptionTier = subscriptionTier;
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
        MaxDevices = subscriptionTier.GetMaxDevices();
        DataRetentionDays = subscriptionTier.GetDataRetentionDays();
    }

    /// <summary>
    /// Factory method to create a new tenant.
    /// </summary>
    public static Tenant Create(
        TenantId id,
        string name,
        string slug,
        SubscriptionTier subscriptionTier,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Tenant slug cannot be empty.", nameof(slug));

        var tenant = new Tenant(id, name, slug, subscriptionTier, createdAt);

        tenant.RaiseDomainEvent(new TenantCreatedEvent(id, name, subscriptionTier, createdAt));

        return tenant;
    }

    /// <summary>
    /// Upgrades the subscription tier and updates quotas.
    /// </summary>
    public void UpgradeSubscription(SubscriptionTier newTier, DateTimeOffset upgradedAt)
    {
        if (newTier <= SubscriptionTier)
            throw new InvalidOperationException($"Cannot upgrade from {SubscriptionTier} to {newTier}.");

        var oldTier = SubscriptionTier;
        SubscriptionTier = newTier;
        MaxDevices = newTier.GetMaxDevices();
        DataRetentionDays = newTier.GetDataRetentionDays();
        UpgradedAt = upgradedAt;

        RaiseDomainEvent(new SubscriptionUpgradedEvent(Id, oldTier, newTier, upgradedAt));
    }

    /// <summary>
    /// Downgrades the subscription tier and updates quotas.
    /// </summary>
    public void DowngradeSubscription(SubscriptionTier newTier, DateTimeOffset downgradedAt)
    {
        if (newTier >= SubscriptionTier)
            throw new InvalidOperationException($"Cannot downgrade from {SubscriptionTier} to {newTier}.");

        SubscriptionTier = newTier;
        MaxDevices = newTier.GetMaxDevices();
        DataRetentionDays = newTier.GetDataRetentionDays();
        UpgradedAt = downgradedAt; // Track last tier change
    }

    /// <summary>
    /// Checks if the tenant can add another device based on current quota.
    /// </summary>
    public bool CanAddDevice(int currentDeviceCount)
    {
        return currentDeviceCount < MaxDevices;
    }

    /// <summary>
    /// Suspends the tenant account.
    /// </summary>
    public void Suspend(string reason)
    {
        if (Status == TenantStatus.Deleted)
            throw new InvalidOperationException("Cannot suspend a deleted tenant.");

        Status = TenantStatus.Suspended;
    }

    /// <summary>
    /// Activates a suspended tenant account.
    /// </summary>
    public void Activate()
    {
        if (Status == TenantStatus.Deleted)
            throw new InvalidOperationException("Cannot activate a deleted tenant.");

        Status = TenantStatus.Active;
    }

    /// <summary>
    /// Soft-deletes the tenant.
    /// </summary>
    public void Delete()
    {
        Status = TenantStatus.Deleted;
    }
}
