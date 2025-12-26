using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout advances to the next phase.
/// </summary>
public record RolloutPhaseAdvancedEvent(
    Guid RolloutId,
    TenantId TenantId,
    int NewPhaseNumber) : DomainEvent;
