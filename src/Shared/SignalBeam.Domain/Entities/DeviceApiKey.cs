using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a device-specific API key for authentication.
/// </summary>
public class DeviceApiKey : Entity<Guid>
{
    /// <summary>
    /// The device this API key belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// BCrypt hash of the API key.
    /// </summary>
    public string KeyHash { get; private set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the key for identification (not sensitive).
    /// </summary>
    public string KeyPrefix { get; private set; } = string.Empty;

    /// <summary>
    /// When this API key expires (null = never expires).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// When this API key was revoked (null = not revoked).
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Last time this API key was used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// When this API key was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Who created this API key (user ID or system).
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// Whether this key is currently active (not revoked and not expired).
    /// </summary>
    public bool IsActive => RevokedAt == null &&
                           (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);

    // EF Core constructor
    private DeviceApiKey() : base(Guid.NewGuid())
    {
    }

    private DeviceApiKey(
        DeviceId deviceId,
        string keyHash,
        string keyPrefix,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        string? createdBy = null) : base(Guid.NewGuid())
    {
        DeviceId = deviceId;
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Factory method to create a new device API key.
    /// </summary>
    public static DeviceApiKey Create(
        DeviceId deviceId,
        string keyHash,
        string keyPrefix,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("Key hash cannot be empty.", nameof(keyHash));

        if (string.IsNullOrWhiteSpace(keyPrefix))
            throw new ArgumentException("Key prefix cannot be empty.", nameof(keyPrefix));

        return new DeviceApiKey(deviceId, keyHash, keyPrefix, createdAt, expiresAt, createdBy);
    }

    /// <summary>
    /// Records that this API key was used for authentication.
    /// </summary>
    public void RecordUsage(DateTimeOffset timestamp)
    {
        LastUsedAt = timestamp;
    }

    /// <summary>
    /// Revokes this API key.
    /// </summary>
    public void Revoke(DateTimeOffset timestamp)
    {
        if (RevokedAt != null)
            throw new InvalidOperationException("API key is already revoked.");

        RevokedAt = timestamp;
    }

    /// <summary>
    /// Checks if the API key is expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset currentTime)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= currentTime;
    }
}
