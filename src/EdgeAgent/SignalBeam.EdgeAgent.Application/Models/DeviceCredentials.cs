namespace SignalBeam.EdgeAgent.Application.Models;

/// <summary>
/// Device credentials stored locally on the edge device.
/// </summary>
public class DeviceCredentials
{
    /// <summary>
    /// Device ID.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Tenant ID.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Device-specific API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// When the API key expires (null = never expires).
    /// </summary>
    public DateTimeOffset? ApiKeyExpiresAt { get; set; }

    /// <summary>
    /// Registration status (Pending, Approved, Rejected).
    /// </summary>
    public string RegistrationStatus { get; set; } = "Pending";

    /// <summary>
    /// When the device was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>
    /// Device name.
    /// </summary>
    public string? DeviceName { get; set; }
}
