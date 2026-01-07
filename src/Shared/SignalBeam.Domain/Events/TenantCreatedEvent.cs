using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a new tenant is created in the system.
/// </summary>
public record TenantCreatedEvent(
    TenantId TenantId,
    string Name,
    SubscriptionTier Tier,
    DateTimeOffset CreatedAt) : DomainEvent;
