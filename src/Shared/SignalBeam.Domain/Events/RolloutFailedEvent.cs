using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout fails.
/// </summary>
public record RolloutFailedEvent(
    Guid RolloutId,
    TenantId TenantId,
    int CurrentPhaseNumber,
    DateTimeOffset FailedAt) : DomainEvent;
