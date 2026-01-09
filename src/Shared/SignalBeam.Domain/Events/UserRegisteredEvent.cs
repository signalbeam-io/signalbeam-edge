using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a new user is registered in the system.
/// </summary>
public record UserRegisteredEvent(
    UserId UserId,
    TenantId TenantId,
    string Email,
    string Name,
    DateTimeOffset RegisteredAt) : DomainEvent;
