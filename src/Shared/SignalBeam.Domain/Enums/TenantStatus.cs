namespace SignalBeam.Domain.Enums;

/// <summary>
/// Tenant account status.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Tenant is active and can use the platform.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Tenant is suspended (e.g., payment failure, policy violation).
    /// </summary>
    Suspended = 1,

    /// <summary>
    /// Tenant has been soft-deleted and marked for cleanup.
    /// </summary>
    Deleted = 2
}
