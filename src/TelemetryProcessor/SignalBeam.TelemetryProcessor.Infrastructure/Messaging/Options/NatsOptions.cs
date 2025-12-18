namespace SignalBeam.TelemetryProcessor.Infrastructure.Messaging.Options;

/// <summary>
/// Configuration options for NATS messaging.
/// </summary>
public class NatsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "NATS";

    /// <summary>
    /// NATS server URL (e.g., "nats://localhost:4222")
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// NATS JetStream configuration
    /// </summary>
    public JetStreamOptions JetStream { get; set; } = new();

    /// <summary>
    /// NATS subject names for telemetry
    /// </summary>
    public SubjectOptions Subjects { get; set; } = new();

    /// <summary>
    /// NATS stream names
    /// </summary>
    public StreamOptions Streams { get; set; } = new();
}

/// <summary>
/// JetStream specific options
/// </summary>
public class JetStreamOptions
{
    /// <summary>
    /// Enable JetStream
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Storage type (File or Memory)
    /// </summary>
    public string StorageType { get; set; } = "File";
}

/// <summary>
/// NATS subject naming configuration
/// </summary>
public class SubjectOptions
{
    /// <summary>
    /// Subject for device metrics (default: signalbeam.telemetry.metrics.>)
    /// </summary>
    public string DeviceMetrics { get; set; } = "signalbeam.telemetry.metrics.>";

    /// <summary>
    /// Subject for device heartbeats (default: signalbeam.devices.heartbeat.>)
    /// </summary>
    public string DeviceHeartbeats { get; set; } = "signalbeam.devices.heartbeat.>";
}

/// <summary>
/// NATS stream naming configuration
/// </summary>
public class StreamOptions
{
    /// <summary>
    /// Stream name for device metrics
    /// </summary>
    public string DeviceMetrics { get; set; } = "DEVICE_METRICS";

    /// <summary>
    /// Stream name for device heartbeats
    /// </summary>
    public string DeviceHeartbeats { get; set; } = "DEVICE_HEARTBEATS";
}
