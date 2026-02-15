using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a user's role within a tenant is changed.
/// </summary>
public record UserRoleChangedEvent(
    UserId UserId,
    TenantId TenantId,
    UserRole OldRole,
    UserRole NewRole,
    DateTimeOffset ChangedAt) : DomainEvent;
