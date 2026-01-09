using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// User aggregate root representing a platform user with tenant association.
/// </summary>
public class User : AggregateRoot<UserId>
{
    /// <summary>
    /// Tenant this user belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// User email address (unique within Zitadel).
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// User display name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// External identity provider user ID (Zitadel subject claim).
    /// </summary>
    public string ZitadelUserId { get; private set; } = string.Empty;

    /// <summary>
    /// User role within the tenant (Admin or DeviceOwner).
    /// </summary>
    public UserRole Role { get; private set; }

    /// <summary>
    /// User account status (Active, Inactive, Deleted).
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// When the user was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Last time the user logged in (UTC).
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    // EF Core constructor
    private User() : base()
    {
    }

    private User(
        UserId id,
        TenantId tenantId,
        string email,
        string name,
        string zitadelUserId,
        UserRole role,
        DateTimeOffset createdAt) : base(id)
    {
        TenantId = tenantId;
        Email = email;
        Name = name;
        ZitadelUserId = zitadelUserId;
        Role = role;
        Status = UserStatus.Active;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a new user.
    /// </summary>
    public static User Create(
        UserId id,
        TenantId tenantId,
        string email,
        string name,
        string zitadelUserId,
        UserRole role,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("User email cannot be empty.", nameof(email));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("User name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(zitadelUserId))
            throw new ArgumentException("Zitadel user ID cannot be empty.", nameof(zitadelUserId));

        var user = new User(id, tenantId, email, name, zitadelUserId, role, createdAt);

        user.RaiseDomainEvent(new UserRegisteredEvent(id, tenantId, email, name, createdAt));

        return user;
    }

    /// <summary>
    /// Updates the user's profile information.
    /// </summary>
    public void UpdateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("User name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// Records a user login timestamp.
    /// </summary>
    public void RecordLogin(DateTimeOffset loginAt)
    {
        LastLoginAt = loginAt;
    }

    /// <summary>
    /// Changes the user's role within the tenant.
    /// </summary>
    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
    }

    /// <summary>
    /// Deactivates the user account.
    /// </summary>
    public void Deactivate()
    {
        if (Status == UserStatus.Deleted)
            throw new InvalidOperationException("Cannot deactivate a deleted user.");

        Status = UserStatus.Inactive;
    }

    /// <summary>
    /// Activates an inactive user account.
    /// </summary>
    public void Activate()
    {
        if (Status == UserStatus.Deleted)
            throw new InvalidOperationException("Cannot activate a deleted user.");

        Status = UserStatus.Active;
    }

    /// <summary>
    /// Soft-deletes the user.
    /// </summary>
    public void Delete()
    {
        Status = UserStatus.Deleted;
    }
}
