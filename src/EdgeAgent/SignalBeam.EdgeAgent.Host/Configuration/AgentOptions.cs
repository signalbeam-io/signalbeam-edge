namespace SignalBeam.EdgeAgent.Host.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string CloudUrl { get; set; } = "https://api.signalbeam.com";
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// How often (seconds) the agent polls for registration approval while still pending.
    /// </summary>
    public int RegistrationPollIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Rotate the API key when it expires within this many days.
    /// </summary>
    public int KeyRotationThresholdDays { get; set; } = 7;

    /// <summary>
    /// How often (hours) the key lifecycle service checks for an expiring API key.
    /// </summary>
    public double KeyLifecycleCheckIntervalHours { get; set; } = 24.0;
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationRetryAttempts { get; set; } = 3;
    public int ReconciliationRetryDelaySeconds { get; set; } = 10;
    public int ImagePullTimeoutSeconds { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
    public string LogFilePath { get; set; } = "/var/log/signalbeam-agent/agent.log";

    /// <summary>
    /// Filesystem mount point whose usage is reported as the device disk metric.
    /// Defaults to the root filesystem; override for devices with a dedicated data partition.
    /// </summary>
    public string MonitoredDiskPath { get; set; } = "/";
}
