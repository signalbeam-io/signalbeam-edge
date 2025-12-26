using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout completes successfully across all phases.
/// </summary>
public record RolloutCompletedEvent(
    Guid RolloutId,
    TenantId TenantId,
    BundleId BundleId,
    BundleVersion TargetVersion,
    DateTimeOffset CompletedAt) : DomainEvent;
