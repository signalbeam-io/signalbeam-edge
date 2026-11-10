using System.Text.Json;
using System.Text.Json.Nodes;

namespace SignalBeam.EdgeAgent.Host.Configuration;

/// <summary>
/// Reads and updates the machine-specific agent config file (<see cref="AgentPaths.ConfigFilePath"/>).
/// </summary>
public static class AgentConfigFile
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Persists <c>Agent:CloudUrl</c> so that later runs (e.g. the systemd service) talk to the
    /// same control plane the device registered with. Other settings in the file are preserved.
    /// </summary>
    public static void SetCloudUrl(string filePath, string cloudUrl)
    {
        JsonObject root = File.Exists(filePath)
            ? JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        if (root[AgentOptions.SectionName] is not JsonObject agent)
        {
            agent = new JsonObject();
            root[AgentOptions.SectionName] = agent;
        }

        agent[nameof(AgentOptions.CloudUrl)] = cloudUrl;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, root.ToJsonString(WriteOptions));
    }
}
