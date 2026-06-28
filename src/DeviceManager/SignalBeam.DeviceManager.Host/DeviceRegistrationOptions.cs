namespace SignalBeam.DeviceManager.Host;

/// <summary>
/// Options for the device-registration handshake. Bound from the <c>Registration</c> configuration
/// section.
/// </summary>
public sealed class DeviceRegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>
    /// When <c>true</c>, the anonymous registration handshake (<c>POST /api/devices</c>) requires a
    /// registration token — tokenless <c>Pending</c> registration is rejected. Default <c>false</c>
    /// preserves the open handshake (a brand-new device can register and await approval). Operators
    /// who don't use tokenless onboarding can enable this to close the anonymous-spam surface
    /// entirely; the EdgeAgent always supplies a token.
    /// </summary>
    public bool RequireRegistrationToken { get; set; }
}
