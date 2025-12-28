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

    // mTLS Certificate Fields

    /// <summary>
    /// Path to client certificate PEM file (for mTLS authentication).
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Path to client private key PEM file (for mTLS authentication).
    /// </summary>
    public string? ClientPrivateKeyPath { get; set; }

    /// <summary>
    /// Path to CA certificate PEM file (for server validation).
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Certificate serial number (for renewal tracking).
    /// </summary>
    public string? CertificateSerialNumber { get; set; }

    /// <summary>
    /// When the client certificate expires (null = no certificate).
    /// </summary>
    public DateTimeOffset? CertificateExpiresAt { get; set; }
}
