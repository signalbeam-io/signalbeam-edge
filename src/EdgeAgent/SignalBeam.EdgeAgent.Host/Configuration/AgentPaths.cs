namespace SignalBeam.EdgeAgent.Host.Configuration;

/// <summary>
/// Well-known filesystem locations used by the agent. These match the directories created by
/// the .deb postinst script and whitelisted in the systemd unit (ReadWritePaths).
/// </summary>
public static class AgentPaths
{
    /// <summary>Writable state: credentials, device state, certificates.</summary>
    public const string DefaultStateDirectory = "/var/lib/signalbeam";

    public const string DefaultLogFilePath = "/var/log/signalbeam/agent.log";

    /// <summary>
    /// Machine-specific settings written by <c>register</c> (e.g. the control-plane URL).
    /// Overrides appsettings.json; environment variables still win.
    /// </summary>
    public const string DefaultConfigDirectory = "/etc/signalbeam";

    public const string ConfigFileName = "agent.json";

    /// <summary>Overrides <see cref="DefaultConfigDirectory"/>, e.g. for local development.</summary>
    public const string ConfigDirectoryEnvironmentVariable = "SIGNALBEAM_CONFIG_DIR";

    public static string ConfigFilePath => Path.Combine(
        Environment.GetEnvironmentVariable(ConfigDirectoryEnvironmentVariable) is { Length: > 0 } dir
            ? dir
            : DefaultConfigDirectory,
        ConfigFileName);
}
