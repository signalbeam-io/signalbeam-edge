using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a new bundle version is created.
/// </summary>
public record BundleVersionCreatedEvent(
    Guid VersionId,
    BundleId BundleId,
    BundleVersion Version,
    int ContainerCount,
    DateTimeOffset CreatedAt) : DomainEvent;
