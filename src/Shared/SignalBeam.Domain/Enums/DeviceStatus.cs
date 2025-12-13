namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the current status of a device.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is registered but hasn't connected yet.
    /// </summary>
    Registered = 0,

    /// <summary>
    /// Device is online and reporting heartbeats.
    /// </summary>
    Online = 1,

    /// <summary>
    /// Device was online but hasn't sent heartbeat within threshold.
    /// </summary>
    Offline = 2,

    /// <summary>
    /// Device is currently updating its bundle.
    /// </summary>
    Updating = 3,

    /// <summary>
    /// Device encountered an error during update or operation.
    /// </summary>
    Error = 4
}
