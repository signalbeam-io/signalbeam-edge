namespace SignalBeam.Domain.Enums;

/// <summary>
/// User account status.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User is active and can access the platform.
    /// </summary>
    Active = 0,

    /// <summary>
    /// User account is inactive (e.g., deactivated by admin).
    /// </summary>
    Inactive = 1,

    /// <summary>
    /// User has been soft-deleted and marked for cleanup.
    /// </summary>
    Deleted = 2
}
