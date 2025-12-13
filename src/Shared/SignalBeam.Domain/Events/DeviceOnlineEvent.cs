using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a device comes online.
/// </summary>
public record DeviceOnlineEvent(DeviceId DeviceId, DateTimeOffset OnlineSince) : DomainEvent;
