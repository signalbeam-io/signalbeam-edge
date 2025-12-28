using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device certificate is issued.
/// </summary>
public sealed record DeviceCertificateIssuedEvent(
    DeviceId DeviceId,
    string SerialNumber,
    string Fingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt) : DomainEvent;
