using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a registration token used for device registration.
/// Tokens are single-use and expire after a certain time.
/// </summary>
public class DeviceRegistrationToken : Entity<Guid>
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    private DeviceRegistrationToken() { } // EF Core
#pragma warning restore CS8618

    private DeviceRegistrationToken(
        Guid id,
        TenantId tenantId,
        string tokenHash,
        string tokenPrefix,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? createdBy,
        string? description,
        int? maxUses)
        : base(id)
    {
        TenantId = tenantId;
        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedBy = createdBy;
        Description = description;
        MaxUses = maxUses;
        CurrentUses = 0;
        IsRevoked = false;
        IsUsed = false;
        UsedAt = null;
        UsedByDeviceId = null;
    }

    /// <summary>
    /// The tenant this token belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// BCrypt hash of the token.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the token for identification (not secret).
    /// Format: sbt_{prefix}
    /// </summary>
    public string TokenPrefix { get; private set; } = string.Empty;

    /// <summary>
    /// When the token was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Who created the token (admin user ID or name).
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// Optional description for the token (e.g., "Factory batch 2024-01").
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Maximum number of times this token can be used (null = unlimited).
    /// </summary>
    public int? MaxUses { get; private set; }

    /// <summary>
    /// Current number of times this token has been used.
    /// </summary>
    public int CurrentUses { get; private set; }

    /// <summary>
    /// Whether the token has been manually revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// When the token was revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Who revoked the token.
    /// </summary>
    public string? RevokedBy { get; private set; }

    /// <summary>
    /// Whether the token has been used (legacy field, kept for backward compatibility).
    /// Use CurrentUses > 0 instead.
    /// </summary>
    public bool IsUsed { get; private set; }

    /// <summary>
    /// When the token was first used (legacy field, kept for backward compatibility).
    /// </summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// The device ID that used this token (legacy field, kept for backward compatibility).
    /// </summary>
    public DeviceId? UsedByDeviceId { get; private set; }

    /// <summary>
    /// Whether the token is currently valid (not expired, not revoked, has remaining uses).
    /// </summary>
    public bool IsValid =>
        !IsRevoked &&
        ExpiresAt > DateTimeOffset.UtcNow &&
        (MaxUses == null || CurrentUses < MaxUses.Value);

    /// <summary>
    /// Whether the token is active (can be displayed in UI).
    /// </summary>
    public bool IsActive => !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new registration token.
    /// </summary>
    public static DeviceRegistrationToken Create(
        TenantId tenantId,
        string tokenHash,
        string tokenPrefix,
        DateTimeOffset expiresAt,
        string? createdBy = null,
        string? description = null,
        int? maxUses = null)
    {
        return new DeviceRegistrationToken(
            Guid.NewGuid(),
            tenantId,
            tokenHash,
            tokenPrefix,
            DateTimeOffset.UtcNow,
            expiresAt,
            createdBy,
            description,
            maxUses);
    }

    /// <summary>
    /// Marks the token as used by a device.
    /// </summary>
    public void MarkAsUsed(DeviceId deviceId)
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("Token has been revoked.");
        }

        if (ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Token has expired.");
        }

        if (MaxUses.HasValue && CurrentUses >= MaxUses.Value)
        {
            throw new InvalidOperationException("Token has reached maximum number of uses.");
        }

        CurrentUses++;

        // Set legacy fields for backward compatibility
        if (CurrentUses == 1)
        {
            IsUsed = true;
            UsedAt = DateTimeOffset.UtcNow;
            UsedByDeviceId = deviceId;
        }
    }

    /// <summary>
    /// Revokes the token, preventing further use.
    /// </summary>
    public void Revoke(string? revokedBy = null)
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("Token has already been revoked.");
        }

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedBy = revokedBy;
    }
}
