using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a paused rollout is resumed.
/// </summary>
public record RolloutResumedEvent(
    Guid RolloutId,
    TenantId TenantId,
    int CurrentPhaseNumber,
    DateTimeOffset ResumedAt) : DomainEvent;
