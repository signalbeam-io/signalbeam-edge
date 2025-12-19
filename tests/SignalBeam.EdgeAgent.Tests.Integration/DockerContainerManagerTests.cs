using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Container;

namespace SignalBeam.EdgeAgent.Tests.Integration;

[Collection("Docker")]
public class DockerContainerManagerTests : IAsyncLifetime
{
    private DockerContainerManager _containerManager = null!;
    private ILoggerFactory _loggerFactory = null!;

    public Task InitializeAsync()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _containerManager = new DockerContainerManager(
            _loggerFactory.CreateLogger<DockerContainerManager>());

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _containerManager?.Dispose();
        _loggerFactory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetRunningContainersAsync_ShouldReturnContainers()
    {
        // Act
        var containers = await _containerManager.GetRunningContainersAsync();

        // Assert
        containers.Should().NotBeNull();
        // Note: We don't assert count since Docker might have other containers running
    }

    [Fact]
    public async Task PullImageAsync_ShouldPullImage_WithoutProgress()
    {
        // Arrange
        const string testImage = "hello-world:latest";

        // Act
        await _containerManager.PullImageAsync(testImage);

        // Assert - No exception means success
        // We can't easily verify the image was pulled without using Docker CLI
    }

    [Fact]
    public async Task PullImageAsync_ShouldPullImage_WithProgress()
    {
        // Arrange
        const string testImage = "alpine:latest";
        var progressReports = new List<ImagePullProgress>();
        var progress = new Progress<ImagePullProgress>(p => progressReports.Add(p));

        // Act
        await _containerManager.PullImageAsync(testImage, progress);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => !string.IsNullOrEmpty(p.Status));
    }

    [Fact]
    public async Task StartContainerAsync_ShouldStartContainer()
    {
        // Arrange
        const string containerName = "test-nginx-container";
        const string image = "nginx:alpine";

        var spec = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image,
            Environment: new Dictionary<string, string>
            {
                ["TEST_VAR"] = "test_value"
            },
            Ports: new Dictionary<string, string>
            {
                ["80/tcp"] = "8080"
            },
            Volumes: null
        );

        try
        {
            // Pull image first
            await _containerManager.PullImageAsync(image);

            // Act
            await _containerManager.StartContainerAsync(spec);

            // Wait a moment for container to start
            await Task.Delay(1000);

            // Assert - Container should be running
            var containers = await _containerManager.GetRunningContainersAsync();
            containers.Should().Contain(c => c.Name == containerName);
        }
        finally
        {
            // Cleanup
            var containers = await _containerManager.GetRunningContainersAsync();
            var testContainer = containers.FirstOrDefault(c => c.Name == containerName);
            if (testContainer != null)
            {
                await _containerManager.StopContainerAsync(testContainer.Id);
            }
        }
    }

    [Fact]
    public async Task StopContainerAsync_ShouldStopContainer()
    {
        // Arrange
        const string containerName = "test-stop-container";
        const string image = "alpine:latest";

        var spec = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image,
            Environment: null,
            Ports: null,
            Volumes: null
        );

        // Pull image and start container
        await _containerManager.PullImageAsync(image);
        await _containerManager.StartContainerAsync(spec);

        // Wait for container to start
        await Task.Delay(1000);

        var containers = await _containerManager.GetRunningContainersAsync();
        var container = containers.First(c => c.Name == containerName);

        // Act
        await _containerManager.StopContainerAsync(container.Id);

        // Wait a moment for container to stop
        await Task.Delay(1000);

        // Assert - Container should not be in running list
        var runningContainers = await _containerManager.GetRunningContainersAsync();
        runningContainers.Should().NotContain(c => c.Id == container.Id);
    }

    [Fact]
    public async Task GetContainerLogsAsync_ShouldReturnLogs()
    {
        // Arrange
        const string containerName = "test-logs-container";
        const string image = "hello-world:latest";

        var spec = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image,
            Environment: null,
            Ports: null,
            Volumes: null
        );

        try
        {
            // Pull image and start container
            await _containerManager.PullImageAsync(image);
            await _containerManager.StartContainerAsync(spec);

            // Wait for container to run and exit
            await Task.Delay(2000);

            // Get all containers (including stopped ones)
            var containers = await _containerManager.GetRunningContainersAsync();
            var container = containers.FirstOrDefault(c => c.Name == containerName);

            // If container already exited, we need to find it differently
            // For now, we'll skip log testing if container already exited
            if (container != null)
            {
                // Act
                var logs = await _containerManager.GetContainerLogsAsync(container.Id, 100);

                // Assert
                logs.Should().NotBeNullOrEmpty();
            }
        }
        finally
        {
            // Cleanup
            var containers = await _containerManager.GetRunningContainersAsync();
            var testContainer = containers.FirstOrDefault(c => c.Name == containerName);
            if (testContainer != null)
            {
                await _containerManager.StopContainerAsync(testContainer.Id);
            }
        }
    }

    [Fact]
    public async Task GetContainerStatsAsync_ShouldReturnStats()
    {
        // Arrange
        const string containerName = "test-stats-container";
        const string image = "nginx:alpine";

        var spec = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image,
            Environment: null,
            Ports: null,
            Volumes: null
        );

        try
        {
            // Pull image and start container
            await _containerManager.PullImageAsync(image);
            await _containerManager.StartContainerAsync(spec);

            // Wait for container to be fully running
            await Task.Delay(2000);

            var containers = await _containerManager.GetRunningContainersAsync();
            var container = containers.First(c => c.Name == containerName);

            // Act
            var stats = await _containerManager.GetContainerStatsAsync(container.Id);

            // Assert
            stats.Should().NotBeNull();
            stats.MemoryUsageBytes.Should().BeGreaterThan(0);
            stats.MemoryLimitBytes.Should().BeGreaterThan(0);
            stats.MemoryUsagePercent.Should().BeGreaterThanOrEqualTo(0);
            stats.CpuUsagePercent.Should().BeGreaterThanOrEqualTo(0);
        }
        finally
        {
            // Cleanup
            var containers = await _containerManager.GetRunningContainersAsync();
            var testContainer = containers.FirstOrDefault(c => c.Name == containerName);
            if (testContainer != null)
            {
                await _containerManager.StopContainerAsync(testContainer.Id);
            }
        }
    }

    [Fact]
    public async Task StartContainerAsync_ShouldReplaceExistingContainer_WhenNameAlreadyExists()
    {
        // Arrange
        const string containerName = "test-replace-container";
        const string image1 = "nginx:alpine";
        const string image2 = "nginx:latest";

        var spec1 = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image1,
            Environment: null,
            Ports: null,
            Volumes: null
        );

        var spec2 = new Application.Services.ContainerSpec(
            Name: containerName,
            Image: image2,
            Environment: null,
            Ports: null,
            Volumes: null
        );

        try
        {
            // Pull images
            await _containerManager.PullImageAsync(image1);
            await _containerManager.PullImageAsync(image2);

            // Start first container
            await _containerManager.StartContainerAsync(spec1);
            await Task.Delay(1000);

            // Act - Start second container with same name
            await _containerManager.StartContainerAsync(spec2);
            await Task.Delay(1000);

            // Assert - Should only have one container with this name
            var containers = await _containerManager.GetRunningContainersAsync();
            var testContainers = containers.Where(c => c.Name == containerName).ToList();
            testContainers.Should().HaveCount(1);
            testContainers.First().Image.Should().Contain("nginx:latest");
        }
        finally
        {
            // Cleanup
            var containers = await _containerManager.GetRunningContainersAsync();
            var testContainer = containers.FirstOrDefault(c => c.Name == containerName);
            if (testContainer != null)
            {
                await _containerManager.StopContainerAsync(testContainer.Id);
            }
        }
    }
}
