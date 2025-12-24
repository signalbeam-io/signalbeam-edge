using System.Text.Json.Serialization;

namespace SignalBeam.BundleOrchestrator.Application.Models;

/// <summary>
/// Bundle definition format for distribution to edge agents.
/// </summary>
public class BundleDefinition
{
    /// <summary>
    /// Bundle identifier.
    /// </summary>
    [JsonPropertyName("bundleId")]
    public string BundleId { get; init; } = string.Empty;

    /// <summary>
    /// Semantic version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Description of the bundle.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// SHA256 checksum of the bundle definition.
    /// </summary>
    [JsonPropertyName("checksum")]
    public string Checksum { get; init; } = string.Empty;

    /// <summary>
    /// Container specifications.
    /// </summary>
    [JsonPropertyName("containers")]
    public List<ContainerDefinition> Containers { get; init; } = new();
}

/// <summary>
/// Container definition within a bundle.
/// </summary>
public class ContainerDefinition
{
    /// <summary>
    /// Container name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Container image reference (e.g., "ghcr.io/signalbeam/temp-sensor:1.2.0").
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; init; } = string.Empty;

    /// <summary>
    /// Image pull policy.
    /// </summary>
    [JsonPropertyName("imagePullPolicy")]
    public string ImagePullPolicy { get; init; } = "IfNotPresent";

    /// <summary>
    /// Environment variables.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; init; }

    /// <summary>
    /// Port mappings.
    /// </summary>
    [JsonPropertyName("ports")]
    public List<PortMapping>? Ports { get; init; }

    /// <summary>
    /// Resource limits and requests.
    /// </summary>
    [JsonPropertyName("resources")]
    public ResourceRequirements? Resources { get; init; }

    /// <summary>
    /// Volume mounts.
    /// </summary>
    [JsonPropertyName("volumes")]
    public List<VolumeMount>? Volumes { get; init; }

    /// <summary>
    /// Restart policy.
    /// </summary>
    [JsonPropertyName("restartPolicy")]
    public string RestartPolicy { get; init; } = "always";
}

/// <summary>
/// Port mapping definition.
/// </summary>
public class PortMapping
{
    /// <summary>
    /// Container port.
    /// </summary>
    [JsonPropertyName("containerPort")]
    public int ContainerPort { get; init; }

    /// <summary>
    /// Host port.
    /// </summary>
    [JsonPropertyName("hostPort")]
    public int HostPort { get; init; }

    /// <summary>
    /// Protocol (tcp, udp).
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "tcp";
}

/// <summary>
/// Resource requirements for a container.
/// </summary>
public class ResourceRequirements
{
    /// <summary>
    /// Resource limits.
    /// </summary>
    [JsonPropertyName("limits")]
    public ResourceSpec? Limits { get; init; }

    /// <summary>
    /// Resource requests.
    /// </summary>
    [JsonPropertyName("requests")]
    public ResourceSpec? Requests { get; init; }
}

/// <summary>
/// Resource specification.
/// </summary>
public class ResourceSpec
{
    /// <summary>
    /// Memory limit (e.g., "128Mi", "1Gi").
    /// </summary>
    [JsonPropertyName("memory")]
    public string? Memory { get; init; }

    /// <summary>
    /// CPU limit (e.g., "0.5", "1").
    /// </summary>
    [JsonPropertyName("cpu")]
    public string? Cpu { get; init; }
}

/// <summary>
/// Volume mount definition.
/// </summary>
public class VolumeMount
{
    /// <summary>
    /// Volume name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Host path.
    /// </summary>
    [JsonPropertyName("hostPath")]
    public string HostPath { get; init; } = string.Empty;

    /// <summary>
    /// Container path.
    /// </summary>
    [JsonPropertyName("containerPath")]
    public string ContainerPath { get; init; } = string.Empty;

    /// <summary>
    /// Read-only flag.
    /// </summary>
    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; init; }
}
