using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a rollout is rolled back to a previous version.
/// </summary>
public record RolloutRolledBackEvent(
    Guid RolloutId,
    TenantId TenantId,
    BundleId BundleId,
    BundleVersion FailedVersion,
    BundleVersion PreviousVersion,
    DateTimeOffset RolledBackAt) : DomainEvent;
