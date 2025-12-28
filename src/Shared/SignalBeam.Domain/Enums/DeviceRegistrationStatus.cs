namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the registration approval status of a device.
/// </summary>
public enum DeviceRegistrationStatus
{
    /// <summary>
    /// Device registration is pending admin approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Device registration has been approved and device can authenticate.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Device registration has been rejected and device cannot authenticate.
    /// </summary>
    Rejected = 2
}
