using SignalBeam.Domain.Abstractions;

namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Container specification defining a Docker container in a bundle.
/// </summary>
public class ContainerSpec : ValueObject
{
    /// <summary>
    /// Container name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Docker image (e.g., "nginx:1.21").
    /// </summary>
    public string Image { get; init; } = string.Empty;

    /// <summary>
    /// Environment variables as JSON.
    /// </summary>
    public string? EnvironmentVariables { get; init; }

    /// <summary>
    /// Port mappings as JSON (e.g., "[{\"host\":8080,\"container\":80}]").
    /// </summary>
    public string? PortMappings { get; init; }

    /// <summary>
    /// Volume mounts as JSON.
    /// </summary>
    public string? VolumeMounts { get; init; }

    /// <summary>
    /// Additional Docker run parameters as JSON.
    /// </summary>
    public string? AdditionalParameters { get; init; }

    private ContainerSpec() { }

    private ContainerSpec(
        string name,
        string image,
        string? environmentVariables = null,
        string? portMappings = null,
        string? volumeMounts = null,
        string? additionalParameters = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Container name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Container image cannot be empty.", nameof(image));

        Name = name;
        Image = image;
        EnvironmentVariables = environmentVariables;
        PortMappings = portMappings;
        VolumeMounts = volumeMounts;
        AdditionalParameters = additionalParameters;
    }

    public static ContainerSpec Create(
        string name,
        string image,
        string? environmentVariables = null,
        string? portMappings = null,
        string? volumeMounts = null,
        string? additionalParameters = null)
    {
        return new ContainerSpec(name, image, environmentVariables, portMappings, volumeMounts, additionalParameters);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Image;
        yield return EnvironmentVariables;
        yield return PortMappings;
        yield return VolumeMounts;
        yield return AdditionalParameters;
    }
}
