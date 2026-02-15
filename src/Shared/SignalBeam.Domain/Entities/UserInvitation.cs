using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Aggregate root representing an invitation for a user to join a tenant.
/// </summary>
public class UserInvitation : AggregateRoot<InvitationId>
{
    /// <summary>
    /// Tenant the user is being invited to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Email address of the invited user.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Role to assign when the invitation is accepted.
    /// </summary>
    public UserRole Role { get; private set; }

    /// <summary>
    /// Current status of the invitation.
    /// </summary>
    public InvitationStatus Status { get; private set; }

    /// <summary>
    /// User who sent the invitation.
    /// </summary>
    public UserId InvitedByUserId { get; private set; }

    /// <summary>
    /// Unique token for accepting/declining the invitation.
    /// </summary>
    public Guid Token { get; private set; }

    /// <summary>
    /// When the invitation was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the invitation expires (UTC).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the invitation was accepted or declined (UTC).
    /// </summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    // EF Core constructor
    private UserInvitation() : base()
    {
    }

    private UserInvitation(
        InvitationId id,
        TenantId tenantId,
        string email,
        UserRole role,
        UserId invitedByUserId,
        Guid token,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) : base(id)
    {
        TenantId = tenantId;
        Email = email;
        Role = role;
        Status = InvitationStatus.Pending;
        InvitedByUserId = invitedByUserId;
        Token = token;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Factory method to create a new user invitation.
    /// </summary>
    public static UserInvitation Create(
        InvitationId id,
        TenantId tenantId,
        string email,
        UserRole role,
        UserId invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var invitation = new UserInvitation(
            id,
            tenantId,
            email,
            role,
            invitedByUserId,
            Guid.NewGuid(),
            createdAt,
            expiresAt);

        invitation.RaiseDomainEvent(new UserInvitedEvent(
            id, tenantId, email, invitedByUserId, createdAt));

        return invitation;
    }

    /// <summary>
    /// Accepts the invitation.
    /// </summary>
    public void Accept(DateTimeOffset respondedAt)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be accepted.");

        if (IsExpired(respondedAt))
            throw new InvalidOperationException("Cannot accept an expired invitation.");

        Status = InvitationStatus.Accepted;
        RespondedAt = respondedAt;
    }

    /// <summary>
    /// Declines the invitation.
    /// </summary>
    public void Decline(DateTimeOffset respondedAt)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be declined.");

        Status = InvitationStatus.Declined;
        RespondedAt = respondedAt;
    }

    /// <summary>
    /// Checks if the invitation has expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
