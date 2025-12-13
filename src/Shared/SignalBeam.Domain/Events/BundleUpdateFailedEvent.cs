using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a bundle update fails.
/// </summary>
public record BundleUpdateFailedEvent(
    DeviceId DeviceId,
    BundleId BundleId,
    DateTimeOffset FailedAt) : DomainEvent;
