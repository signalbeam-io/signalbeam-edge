using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout is started.
/// </summary>
public record RolloutStartedEvent(
    Guid RolloutId,
    TenantId TenantId,
    BundleId BundleId,
    BundleVersion TargetVersion,
    DateTimeOffset StartedAt) : DomainEvent;
