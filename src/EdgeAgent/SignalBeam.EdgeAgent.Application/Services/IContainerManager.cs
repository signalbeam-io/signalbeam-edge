namespace SignalBeam.EdgeAgent.Application.Services;

public interface IContainerManager
{
    Task<List<ContainerStatus>> GetRunningContainersAsync(CancellationToken cancellationToken = default);
    Task PullImageAsync(string image, IProgress<ImagePullProgress>? progress = null, CancellationToken cancellationToken = default);
    Task StartContainerAsync(ContainerSpec spec, CancellationToken cancellationToken = default);
    Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<string> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default);
    Task<ContainerStats> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);
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

public record ImagePullProgress(
    string Status,
    string? Id = null,
    long? Current = null,
    long? Total = null);

public record ContainerStats(
    double CpuUsagePercent,
    long MemoryUsageBytes,
    long MemoryLimitBytes,
    double MemoryUsagePercent,
    long NetworkRxBytes,
    long NetworkTxBytes,
    long BlockReadBytes,
    long BlockWriteBytes);
