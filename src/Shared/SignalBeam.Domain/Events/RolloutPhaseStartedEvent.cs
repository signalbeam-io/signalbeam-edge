using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout phase is started.
/// </summary>
public record RolloutPhaseStartedEvent(
    Guid RolloutId,
    TenantId TenantId,
    Guid PhaseId,
    int PhaseNumber,
    DateTimeOffset StartedAt) : DomainEvent;
