using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a tenant's subscription tier is upgraded.
/// </summary>
public record SubscriptionUpgradedEvent(
    TenantId TenantId,
    SubscriptionTier OldTier,
    SubscriptionTier NewTier,
    DateTimeOffset UpgradedAt) : DomainEvent;
