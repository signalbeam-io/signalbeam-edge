using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device registration is approved.
/// </summary>
public sealed record DeviceRegistrationApprovedEvent(
    DeviceId DeviceId,
    DateTimeOffset ApprovedAt) : DomainEvent;
