namespace SignalBeam.EdgeAgent.Host.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string CloudUrl { get; set; } = "https://api.signalbeam.com";
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationRetryAttempts { get; set; } = 3;
    public int ReconciliationRetryDelaySeconds { get; set; } = 10;
    public int ImagePullTimeoutSeconds { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
    public string LogFilePath { get; set; } = "/var/log/signalbeam-agent/agent.log";
}
