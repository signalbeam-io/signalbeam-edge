using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout phase completes successfully.
/// </summary>
public record RolloutPhaseCompletedEvent(
    Guid RolloutId,
    TenantId TenantId,
    Guid PhaseId,
    int PhaseNumber,
    DateTimeOffset CompletedAt) : DomainEvent;
