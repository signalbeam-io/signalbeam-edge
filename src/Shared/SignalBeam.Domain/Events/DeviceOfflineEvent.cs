using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a device goes offline.
/// </summary>
public record DeviceOfflineEvent(DeviceId DeviceId, DateTimeOffset OfflineSince) : DomainEvent;
