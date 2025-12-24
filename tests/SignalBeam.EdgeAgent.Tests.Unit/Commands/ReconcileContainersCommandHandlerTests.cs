using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using Microsoft.Extensions.Logging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Commands;

public class ReconcileContainersCommandHandlerTests
{
    private readonly IContainerManager _containerManager;
    private readonly ReconcileContainersCommandHandler _handler;

    public ReconcileContainersCommandHandlerTests()
    {
        _containerManager = Substitute.For<IContainerManager>();
        var logger = Substitute.For<ILogger<ReconcileContainersCommandHandler>>();
        _handler = new ReconcileContainersCommandHandler(_containerManager, logger);
    }

    [Fact]
    public async Task Handle_NoDesiredState_StopsAllContainers()
    {
        // Arrange
        var runningContainers = new List<ContainerStatus>
        {
            new("container-1", "app1", "image1:v1", "running", DateTime.UtcNow),
            new("container-2", "app2", "image2:v1", "running", DateTime.UtcNow)
        };

        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns(runningContainers);

        var command = new ReconcileContainersCommand(Guid.NewGuid(), null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContainersStopped.Should().Be(2);
        await _containerManager.Received(2).StopContainerAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewContainerInDesiredState_StartsContainer()
    {
        // Arrange
        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContainerStatus>());

        var desiredState = new DesiredState(
            "bundle-1",
            "1.0.0",
            new List<ContainerSpec>
            {
                new("app1", "image1:v1")
            });

        var command = new ReconcileContainersCommand(Guid.NewGuid(), desiredState);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContainersStarted.Should().Be(1);
        await _containerManager.Received(1).PullImageAsync("image1:v1", null, Arg.Any<CancellationToken>());
        await _containerManager.Received(1).StartContainerAsync(
            Arg.Is<ContainerSpec>(c => c.Name == "app1" && c.Image == "image1:v1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ContainerNotInDesiredState_StopsContainer()
    {
        // Arrange
        var runningContainers = new List<ContainerStatus>
        {
            new("container-1", "app1", "image1:v1", "running", DateTime.UtcNow),
            new("container-2", "app2", "image2:v1", "running", DateTime.UtcNow)
        };

        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns(runningContainers);

        var desiredState = new DesiredState(
            "bundle-1",
            "1.0.0",
            new List<ContainerSpec>
            {
                new("app1", "image1:v1") // Only app1 in desired state
            });

        var command = new ReconcileContainersCommand(Guid.NewGuid(), desiredState);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContainersStopped.Should().Be(1);
        await _containerManager.Received(1).StopContainerAsync("container-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImageChanged_StopsOldAndStartsNew()
    {
        // Arrange
        var runningContainers = new List<ContainerStatus>
        {
            new("container-1", "app1", "image1:v1", "running", DateTime.UtcNow)
        };

        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns(runningContainers);

        var desiredState = new DesiredState(
            "bundle-1",
            "1.0.0",
            new List<ContainerSpec>
            {
                new("app1", "image1:v2") // Updated image version
            });

        var command = new ReconcileContainersCommand(Guid.NewGuid(), desiredState);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContainersStarted.Should().Be(1);
        result.Value.ContainersStopped.Should().Be(1);
        await _containerManager.Received(1).StopContainerAsync("container-1", Arg.Any<CancellationToken>());
        await _containerManager.Received(1).PullImageAsync("image1:v2", null, Arg.Any<CancellationToken>());
        await _containerManager.Received(1).StartContainerAsync(
            Arg.Is<ContainerSpec>(c => c.Image == "image1:v2"),
            Arg.Any<CancellationToken>());
    }
}
