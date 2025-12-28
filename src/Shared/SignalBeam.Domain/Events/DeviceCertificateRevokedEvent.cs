using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device certificate is revoked.
/// </summary>
public sealed record DeviceCertificateRevokedEvent(
    DeviceId DeviceId,
    string SerialNumber,
    DateTimeOffset RevokedAt,
    string? Reason = null) : DomainEvent;
