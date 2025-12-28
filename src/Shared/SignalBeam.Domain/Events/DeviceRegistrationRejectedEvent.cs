using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device registration is rejected.
/// </summary>
public sealed record DeviceRegistrationRejectedEvent(
    DeviceId DeviceId,
    DateTimeOffset RejectedAt,
    string? Reason = null) : DomainEvent;
