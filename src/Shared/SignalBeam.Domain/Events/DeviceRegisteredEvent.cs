using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a new device is registered in the system.
/// </summary>
public record DeviceRegisteredEvent(
    DeviceId DeviceId,
    TenantId TenantId,
    string DeviceName,
    DateTimeOffset RegisteredAt) : DomainEvent;
