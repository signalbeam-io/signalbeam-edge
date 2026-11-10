using System.Text.Json.Nodes;
using SignalBeam.EdgeAgent.Host;
using SignalBeam.EdgeAgent.Host.Configuration;

namespace SignalBeam.EdgeAgent.Tests.Unit.Configuration;

public sealed class AgentConfigurationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sb-agent-config-{Guid.NewGuid():N}");

    private string ConfigFile => Path.Combine(_dir, AgentPaths.ConfigFileName);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void SetCloudUrl_CreatesFileAndDirectory_WhenMissing()
    {
        AgentConfigFile.SetCloudUrl(ConfigFile, "http://mac.tailnet:8080");

        var json = JsonNode.Parse(File.ReadAllText(ConfigFile))!;
        json["Agent"]!["CloudUrl"]!.GetValue<string>().Should().Be("http://mac.tailnet:8080");
    }

    [Fact]
    public void SetCloudUrl_PreservesOtherSettings()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigFile, """
            { "Agent": { "CloudUrl": "http://old", "HeartbeatIntervalSeconds": 15 }, "Other": { "Key": "v" } }
            """);

        AgentConfigFile.SetCloudUrl(ConfigFile, "http://new");

        var json = JsonNode.Parse(File.ReadAllText(ConfigFile))!;
        json["Agent"]!["CloudUrl"]!.GetValue<string>().Should().Be("http://new");
        json["Agent"]!["HeartbeatIntervalSeconds"]!.GetValue<int>().Should().Be(15);
        json["Other"]!["Key"]!.GetValue<string>().Should().Be("v");
    }

    [Fact]
    public void BuildConfiguration_LoadsAppSettingsFromBinaryDirectory_RegardlessOfWorkingDirectory()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(_dir);
        try
        {
            // Simulates systemd's WorkingDirectory=/var/lib/signalbeam, which has no appsettings.json.
            Directory.SetCurrentDirectory(_dir);

            var configuration = HostBuilder.BuildConfiguration(ConfigFile);

            configuration["Agent:StateDirectory"].Should().Be(AgentPaths.DefaultStateDirectory);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void BuildConfiguration_MachineConfigFile_OverridesAppSettingsCloudUrl()
    {
        AgentConfigFile.SetCloudUrl(ConfigFile, "http://registered-control-plane:8080");

        var configuration = HostBuilder.BuildConfiguration(ConfigFile);

        configuration["Agent:CloudUrl"].Should().Be("http://registered-control-plane:8080");
    }

    [Fact]
    public void AppSettings_UsesDirectoriesTheServiceMayWrite()
    {
        // The systemd unit only allows writes under /var/lib/signalbeam and /var/log/signalbeam.
        var configuration = HostBuilder.BuildConfiguration(ConfigFile);

        configuration["Agent:CredentialsDirectory"].Should().Be("/var/lib/signalbeam");
        configuration["Agent:StateDirectory"].Should().Be("/var/lib/signalbeam");
        configuration["Agent:LogFilePath"].Should().StartWith("/var/log/signalbeam/");
    }
}
