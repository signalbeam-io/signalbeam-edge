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

    private static ContainerStatus Managed(string id, string name, string image) =>
        new(id, name, image, "running", DateTime.UtcNow, IsManaged: true, ManagedImage: image);

    private void ManagedRunning(params ContainerStatus[] containers) =>
        _containerManager.GetManagedContainersAsync(Arg.Any<CancellationToken>()).Returns(containers.ToList());

    private static ReconcileContainersCommand Command(params ContainerSpec[] containers) =>
        new(Guid.NewGuid(), new DesiredState("bundle-1", "1.0.0", containers.ToList()));

    [Fact]
    public async Task Handle_NoDesiredState_LeavesContainersAlone()
    {
        // Act
        var result = await _handler.Handle(new ReconcileContainersCommand(Guid.NewGuid(), null), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersStopped.Should().Be(0);
        await _containerManager.DidNotReceive().GetManagedContainersAsync(Arg.Any<CancellationToken>());
        await _containerManager.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DesiredStateWithNoContainers_StopsOnlyManagedContainers()
    {
        // Arrange
        ManagedRunning(Managed("container-1", "app1", "image1:v1"), Managed("container-2", "app2", "image2:v1"));

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersStopped.Should().Be(2);
        await _containerManager.Received(1).StopContainerAsync("container-1", Arg.Any<CancellationToken>());
        await _containerManager.Received(1).StopContainerAsync("container-2", Arg.Any<CancellationToken>());
        await _containerManager.DidNotReceive().GetRunningContainersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NeverConsultsUnmanagedContainers()
    {
        // Arrange: the host also runs the agent itself and unrelated containers; reconciliation
        // must only ever see the managed list.
        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new("agent", "signalbeam-agent", "ghcr.io/signalbeam-io/edge-agent:1.0", "running", DateTime.UtcNow),
            new("other", "home-assistant", "homeassistant/home-assistant", "running", DateTime.UtcNow)
        ]);
        ManagedRunning();

        // Act
        await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        await _containerManager.DidNotReceive().GetRunningContainersAsync(Arg.Any<CancellationToken>());
        await _containerManager.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewContainerInDesiredState_StartsItLabelledWithItsBundle()
    {
        // Arrange
        ManagedRunning();

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersStarted.Should().Be(1);
        await _containerManager.Received(1).PullImageAsync("image1:v1", null, Arg.Any<CancellationToken>());
        await _containerManager.Received(1).StartContainerAsync(
            Arg.Is<ContainerSpec>(c => c.Name == "app1" && c.Image == "image1:v1"),
            new ContainerOwner("bundle-1", "1.0.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManagedContainerNotInDesiredState_StopsIt()
    {
        // Arrange
        ManagedRunning(Managed("container-1", "app1", "image1:v1"), Managed("container-2", "app2", "image2:v1"));

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersStopped.Should().Be(1);
        await _containerManager.Received(1).StopContainerAsync("container-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImageChanged_StopsOldAndStartsNew()
    {
        // Arrange
        ManagedRunning(Managed("container-1", "app1", "image1:v1"));

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v2")), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersStarted.Should().Be(1);
        result.Value.ContainersStopped.Should().Be(1);
        await _containerManager.Received(1).StopContainerAsync("container-1", Arg.Any<CancellationToken>());
        await _containerManager.Received(1).PullImageAsync("image1:v2", null, Arg.Any<CancellationToken>());
        await _containerManager.Received(1).StartContainerAsync(
            Arg.Is<ContainerSpec>(c => c.Image == "image1:v2"), Arg.Any<ContainerOwner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DockerReportsImageId_ComparesAgainstTheRequestedImage()
    {
        // Arrange: Docker shows an image ID once the tag has moved; the label keeps what was asked for.
        ManagedRunning(new ContainerStatus(
            "container-1", "app1", "sha256:0123abcd", "running", DateTime.UtcNow, IsManaged: true, ManagedImage: "image1:v1"));

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.Value!.ContainersStarted.Should().Be(0);
        result.Value.ContainersStopped.Should().Be(0);
        await _containerManager.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameTakenByUnmanagedContainer_ReportsFailureAndLeavesItAlone()
    {
        // Arrange
        ManagedRunning();
        _containerManager
            .StartContainerAsync(Arg.Any<ContainerSpec>(), Arg.Any<ContainerOwner>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ContainerNameConflictException("app1")));

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContainersFailed.Should().Be(1);
        result.Value.Errors.Should().ContainSingle().Which.Should().Contain("not created by SignalBeam");
        await _containerManager.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameHeldByUnmanagedContainer_ReportsConflictWithoutPulling()
    {
        // Arrange: e.g. a container left by an agent that predates labels.
        ManagedRunning();
        _containerManager.IsNameHeldByUnmanagedContainerAsync("app1", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.Value!.ContainersFailed.Should().Be(1);
        result.Value.Errors.Should().ContainSingle().Which.Should().StartWith(ContainerNameConflictException.ErrorCode);
        await _containerManager.DidNotReceive().PullImageAsync(Arg.Any<string>(), Arg.Any<IProgress<ImagePullProgress>?>(), Arg.Any<CancellationToken>());
        await _containerManager.DidNotReceive().StartContainerAsync(Arg.Any<ContainerSpec>(), Arg.Any<ContainerOwner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManagedContainerWithoutImageLabel_ComparesAgainstDockersImage()
    {
        // Arrange
        ManagedRunning(new ContainerStatus("container-1", "app1", "image1:v1", "running", DateTime.UtcNow, IsManaged: true));

        // Act
        var result = await _handler.Handle(Command(new ContainerSpec("app1", "image1:v1")), CancellationToken.None);

        // Assert
        result.Value!.ContainersStarted.Should().Be(0);
        await _containerManager.DidNotReceive().StopContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
