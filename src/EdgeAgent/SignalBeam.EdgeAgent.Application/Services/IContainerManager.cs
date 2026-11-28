namespace SignalBeam.EdgeAgent.Application.Services;

public interface IContainerManager
{
    // Every running container on the host, for metrics and state reports.
    Task<List<ContainerStatus>> GetRunningContainersAsync(CancellationToken cancellationToken = default);

    // Only running containers the agent created (see ManagedContainerLabels). Reconciliation must use
    // this, so the agent never stops its own container or anything else on the host.
    Task<List<ContainerStatus>> GetManagedContainersAsync(CancellationToken cancellationToken = default);

    Task PullImageAsync(string image, IProgress<ImagePullProgress>? progress = null, CancellationToken cancellationToken = default);

    // Whether a container the agent didn't create already has this exact name.
    Task<bool> IsNameHeldByUnmanagedContainerAsync(string name, CancellationToken cancellationToken = default);

    // Throws ContainerNameConflictException when the name belongs to a container the agent doesn't manage.
    Task StartContainerAsync(ContainerSpec spec, ContainerOwner owner, CancellationToken cancellationToken = default);
    Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<string> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default);
    Task<ContainerStats> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default);
}

// ManagedImage is the image the agent asked for, from the container's label. Docker may report
// Image as an ID instead of the reference it was created from, so compare against ManagedImage.
public record ContainerStatus(
    string Id,
    string Name,
    string Image,
    string State,
    DateTime CreatedAt,
    bool IsManaged = false,
    string? ManagedImage = null);

// The bundle a managed container belongs to.
public record ContainerOwner(string? BundleId, string? BundleVersion);

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
