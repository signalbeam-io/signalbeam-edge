using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a bundle is assigned to a device.
/// </summary>
public record BundleAssignedEvent(
    DeviceId DeviceId,
    BundleId BundleId,
    DateTimeOffset AssignedAt) : DomainEvent;
