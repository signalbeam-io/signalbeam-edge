using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Events;

/// <summary>
/// Raised when a user is invited to join a tenant.
/// </summary>
public record UserInvitedEvent(
    InvitationId InvitationId,
    TenantId TenantId,
    string Email,
    UserId InvitedByUserId,
    DateTimeOffset InvitedAt) : DomainEvent;
