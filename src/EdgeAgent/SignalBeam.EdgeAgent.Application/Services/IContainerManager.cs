namespace SignalBeam.EdgeAgent.Application.Services;

public interface IContainerManager
{
    Task<List<ContainerStatus>> GetRunningContainersAsync(CancellationToken cancellationToken = default);
    Task PullImageAsync(string image, CancellationToken cancellationToken = default);
    Task StartContainerAsync(ContainerSpec spec, CancellationToken cancellationToken = default);
    Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default);
}

public record ContainerStatus(
    string Id,
    string Name,
    string Image,
    string State,
    DateTime CreatedAt);

public record ContainerSpec(
    string Name,
    string Image,
    Dictionary<string, string>? Environment = null,
    Dictionary<string, string>? Ports = null,
    Dictionary<string, string>? Volumes = null);
