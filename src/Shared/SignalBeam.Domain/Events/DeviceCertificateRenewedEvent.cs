using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device certificate is renewed.
/// </summary>
public sealed record DeviceCertificateRenewedEvent(
    DeviceId DeviceId,
    string OldSerialNumber,
    string NewSerialNumber,
    string NewFingerprint,
    DateTimeOffset RenewedAt,
    DateTimeOffset ExpiresAt) : DomainEvent;
