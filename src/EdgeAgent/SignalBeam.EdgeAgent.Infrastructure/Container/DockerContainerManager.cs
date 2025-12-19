using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Container;

public class DockerContainerManager : IContainerManager, IDisposable
{
    private readonly IDockerClient _client;
    private readonly ILogger<DockerContainerManager> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public DockerContainerManager(ILogger<DockerContainerManager> logger)
    {
        _logger = logger;

        // Initialize Docker client
        var dockerUri = Environment.GetEnvironmentVariable("DOCKER_HOST")
            ?? (OperatingSystem.IsWindows()
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock");

        _client = new DockerClientConfiguration(new Uri(dockerUri))
            .CreateClient();

        _logger.LogInformation("Docker client initialized with endpoint: {Endpoint}", dockerUri);

        // Configure retry policy for transient failures
        _retryPolicy = Policy
            .Handle<DockerApiException>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Docker operation failed (attempt {RetryCount}). Retrying in {RetryDelay}s",
                        retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task<List<Application.Services.ContainerStatus>> GetRunningContainersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching running containers");

            var containers = await _retryPolicy.ExecuteAsync(async () =>
                await _client.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        All = false
                    },
                    cancellationToken));

            var result = containers.Select(c => new Application.Services.ContainerStatus(
                Id: c.ID,
                Name: c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12],
                Image: c.Image,
                State: c.State,
                CreatedAt: c.Created
            )).ToList();

            _logger.LogInformation("Found {Count} running containers", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get running containers");
            throw;
        }
    }

    public async Task PullImageAsync(
        string image,
        IProgress<ImagePullProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Pulling image: {Image}", image);

            var pullProgress = new Progress<JSONMessage>(message =>
            {
                var current = message.Progress?.Current;
                var total = message.Progress?.Total;

                progress?.Report(new ImagePullProgress(
                    Status: message.Status ?? string.Empty,
                    Id: message.ID,
                    Current: current,
                    Total: total
                ));

                if (!string.IsNullOrEmpty(message.Status))
                {
                    _logger.LogDebug("Image pull progress: {Status} {Id}", message.Status, message.ID);
                }
            });

            await _retryPolicy.ExecuteAsync(async () =>
                await _client.Images.CreateImageAsync(
                    new ImagesCreateParameters
                    {
                        FromImage = image
                    },
                    null,
                    pullProgress,
                    cancellationToken));

            _logger.LogInformation("Successfully pulled image: {Image}", image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull image: {Image}", image);
            throw;
        }
    }

    public async Task StartContainerAsync(
        Application.Services.ContainerSpec spec,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting container: {Name} with image {Image}", spec.Name, spec.Image);

            // Check if container with same name already exists
            var existingContainers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [spec.Name] = true }
                    }
                },
                cancellationToken);

            // Remove existing container if it exists
            if (existingContainers.Any())
            {
                var existingContainer = existingContainers.First();
                _logger.LogInformation("Removing existing container: {ContainerId}", existingContainer.ID);

                await _client.Containers.RemoveContainerAsync(
                    existingContainer.ID,
                    new ContainerRemoveParameters { Force = true },
                    cancellationToken);
            }

            // Create container
            var createResponse = await _retryPolicy.ExecuteAsync(async () =>
                await _client.Containers.CreateContainerAsync(
                    new CreateContainerParameters
                    {
                        Name = spec.Name,
                        Image = spec.Image,
                        Env = spec.Environment?.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),
                        HostConfig = new HostConfig
                        {
                            PortBindings = spec.Ports?.ToDictionary(
                                kvp => kvp.Key,
                                kvp => (IList<PortBinding>)new List<PortBinding>
                                {
                                    new() { HostPort = kvp.Value }
                                }
                            ),
                            Binds = spec.Volumes?.Select(kvp => $"{kvp.Key}:{kvp.Value}").ToList()
                        }
                    },
                    cancellationToken));

            // Start container
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var started = await _client.Containers.StartContainerAsync(
                    createResponse.ID,
                    new ContainerStartParameters(),
                    cancellationToken);

                return started;
            });

            _logger.LogInformation("Successfully started container: {Name} (ID: {ContainerId})",
                spec.Name, createResponse.ID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start container: {Name}", spec.Name);
            throw;
        }
    }

    public async Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping container: {ContainerId}", containerId);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var stopped = await _client.Containers.StopContainerAsync(
                    containerId,
                    new ContainerStopParameters
                    {
                        WaitBeforeKillSeconds = 10
                    },
                    cancellationToken);

                return stopped;
            });

            _logger.LogInformation("Successfully stopped container: {ContainerId}", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop container: {ContainerId}", containerId);
            throw;
        }
    }

    public async Task<string> GetContainerLogsAsync(
        string containerId,
        int tailLines = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching logs for container: {ContainerId} (tail: {TailLines})",
                containerId, tailLines);

            var logs = await _retryPolicy.ExecuteAsync(async () =>
                await _client.Containers.GetContainerLogsAsync(
                    containerId,
                    false, // tty - set to false for proper demultiplexing
                    new ContainerLogsParameters
                    {
                        ShowStdout = true,
                        ShowStderr = true,
                        Tail = tailLines.ToString(),
                        Timestamps = true
                    },
                    cancellationToken));

            using var memoryStream = new MemoryStream();
            await logs.CopyOutputToAsync(Stream.Null, memoryStream, Stream.Null, cancellationToken);
            memoryStream.Position = 0;

            using var reader = new StreamReader(memoryStream);
            var logContent = await reader.ReadToEndAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Length} bytes of logs for container: {ContainerId}",
                logContent.Length, containerId);

            return logContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs for container: {ContainerId}", containerId);
            throw;
        }
    }

    public async Task<Application.Services.ContainerStats> GetContainerStatsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching stats for container: {ContainerId}", containerId);

            ContainerStatsResponse? statsResponse = null;
            var statsProgress = new Progress<ContainerStatsResponse>(stats =>
            {
                statsResponse = stats;
            });

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _client.Containers.GetContainerStatsAsync(
                    containerId,
                    new ContainerStatsParameters
                    {
                        Stream = false
                    },
                    statsProgress,
                    cancellationToken);

                // Give the progress callback time to be invoked
                await Task.Delay(100, cancellationToken);

                return true;
            });

            if (statsResponse == null)
            {
                throw new InvalidOperationException($"No stats available for container {containerId}");
            }

            // Calculate CPU percentage
            var cpuDelta = statsResponse.CPUStats.CPUUsage.TotalUsage -
                           statsResponse.PreCPUStats.CPUUsage.TotalUsage;
            var systemDelta = statsResponse.CPUStats.SystemUsage -
                              statsResponse.PreCPUStats.SystemUsage;
            var cpuPercent = systemDelta > 0
                ? (double)cpuDelta / systemDelta * 100.0
                : 0.0;

            // Calculate memory percentage
            var memoryUsage = statsResponse.MemoryStats.Usage;
            var memoryLimit = statsResponse.MemoryStats.Limit;
            var memoryPercent = memoryLimit > 0
                ? (double)memoryUsage / memoryLimit * 100.0
                : 0.0;

            // Calculate network I/O
            var networkRx = statsResponse.Networks?.Values.Sum(n => (long)n.RxBytes) ?? 0;
            var networkTx = statsResponse.Networks?.Values.Sum(n => (long)n.TxBytes) ?? 0;

            // Calculate block I/O
            var blockRead = statsResponse.BlkioStats.IoServiceBytesRecursive?
                .Where(s => s.Op == "read")
                .Sum(s => (long)s.Value) ?? 0;
            var blockWrite = statsResponse.BlkioStats.IoServiceBytesRecursive?
                .Where(s => s.Op == "write")
                .Sum(s => (long)s.Value) ?? 0;

            var result = new Application.Services.ContainerStats(
                CpuUsagePercent: cpuPercent,
                MemoryUsageBytes: (long)memoryUsage,
                MemoryLimitBytes: (long)memoryLimit,
                MemoryUsagePercent: memoryPercent,
                NetworkRxBytes: networkRx,
                NetworkTxBytes: networkTx,
                BlockReadBytes: blockRead,
                BlockWriteBytes: blockWrite
            );

            _logger.LogDebug(
                "Container {ContainerId} stats: CPU {Cpu:F2}%, Memory {Memory:F2}%",
                containerId, result.CpuUsagePercent, result.MemoryUsagePercent);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats for container: {ContainerId}", containerId);
            throw;
        }
    }

    private static bool IsTransientError(DockerApiException exception)
    {
        // Retry on network errors, timeouts, and server errors (5xx)
        return exception.StatusCode >= System.Net.HttpStatusCode.InternalServerError ||
               exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
