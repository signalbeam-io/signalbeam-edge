using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout is paused.
/// </summary>
public record RolloutPausedEvent(
    Guid RolloutId,
    TenantId TenantId,
    int CurrentPhaseNumber) : DomainEvent;
