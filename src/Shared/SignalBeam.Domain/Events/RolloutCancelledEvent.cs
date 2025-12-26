using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout is cancelled.
/// </summary>
public record RolloutCancelledEvent(
    Guid RolloutId,
    TenantId TenantId,
    DateTimeOffset CancelledAt) : DomainEvent;
