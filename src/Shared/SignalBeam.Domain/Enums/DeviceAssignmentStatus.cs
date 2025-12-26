namespace SignalBeam.Domain.Enums;

/// <summary>
/// Status of a device assignment within a rollout phase.
/// </summary>
public enum DeviceAssignmentStatus
{
    /// <summary>
    /// Device assigned but rollout not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Device has been assigned the bundle (desired state updated).
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// Agent is reconciling (downloading/deploying containers).
    /// </summary>
    Reconciling = 2,

    /// <summary>
    /// Rollout succeeded on this device.
    /// </summary>
    Succeeded = 3,

    /// <summary>
    /// Rollout failed on this device.
    /// </summary>
    Failed = 4
}
