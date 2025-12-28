using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device API key is revoked.
/// </summary>
public sealed record DeviceApiKeyRevokedEvent(
    DeviceId DeviceId,
    Guid ApiKeyId,
    DateTimeOffset RevokedAt) : DomainEvent;
