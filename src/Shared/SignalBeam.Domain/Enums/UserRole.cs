namespace SignalBeam.Domain.Enums;

/// <summary>
/// User role within a tenant for authorization and access control.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Device owner who can manage their own devices and view data.
    /// </summary>
    DeviceOwner = 0,

    /// <summary>
    /// Administrator who can manage tenant settings, invite users, and handle billing.
    /// </summary>
    Admin = 1
}
