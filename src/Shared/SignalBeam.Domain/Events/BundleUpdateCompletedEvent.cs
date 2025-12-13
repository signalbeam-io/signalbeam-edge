using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a bundle update completes successfully.
/// </summary>
public record BundleUpdateCompletedEvent(
    DeviceId DeviceId,
    BundleId BundleId,
    DateTimeOffset CompletedAt) : DomainEvent;
