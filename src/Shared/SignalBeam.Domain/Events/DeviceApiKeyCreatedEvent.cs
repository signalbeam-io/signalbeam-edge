using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Domain event raised when a device API key is created.
/// </summary>
public sealed record DeviceApiKeyCreatedEvent(
    DeviceId DeviceId,
    Guid ApiKeyId,
    string KeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt = null) : DomainEvent;
