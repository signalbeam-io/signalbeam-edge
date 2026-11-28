using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Container;
using ContainerSpec = SignalBeam.EdgeAgent.Application.Services.ContainerSpec;

namespace SignalBeam.EdgeAgent.Tests.Integration;

// #494: reconciliation must only touch containers the agent created. Containers it didn't create
// (the agent's own, Home Assistant, anything else on the Pi) are started here without the label
// and must still be running afterwards.
//
// Reconciling stops every managed container not in the desired state, including ones that aren't
// this test's (a local agent's workloads). So the tests refuse to run while any managed container
// other than their own exists on this Docker host.
[Collection("Docker")]
public class ManagedContainerReconciliationTests : IAsyncLifetime
{
    private const string Image = "nginx:alpine";

    private readonly string _prefix = $"sb-it-{Guid.NewGuid():N}"[..14];
    private readonly List<IContainer> _unmanaged = [];
    private readonly List<string> _createdNames = [];
    private DockerContainerManager _manager = null!;
    private ReconcileContainersCommandHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _manager = new DockerContainerManager(NullLogger<DockerContainerManager>.Instance);
        _handler = new ReconcileContainersCommandHandler(_manager, NullLogger<ReconcileContainersCommandHandler>.Instance);

        var foreign = (await _manager.GetManagedContainersAsync()).Where(c => !c.Name.StartsWith("sb-it-", StringComparison.Ordinal)).ToList();
        if (foreign.Count > 0)
        {
            throw new InvalidOperationException(
                "Refusing to run: these tests would stop SignalBeam-managed containers on this Docker host: "
                + string.Join(", ", foreign.Select(c => c.Name)));
        }

        await _manager.PullImageAsync(Image);
    }

    public async Task DisposeAsync()
    {
        foreach (var container in _unmanaged)
        {
            await container.DisposeAsync();
        }

        using var client = new DockerClientBuilder().Build();
        foreach (var name in _createdNames)
        {
            var existing = await client.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>> { ["name"] = new Dictionary<string, bool> { [name] = true } }
            });
            foreach (var container in existing.Where(c => c.Names.Contains($"/{name}")))
            {
                await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters { Force = true });
            }
        }

        _manager.Dispose();
    }

    [Fact]
    public async Task Reconcile_LeavesUnmanagedContainersRunning()
    {
        // Arrange
        var bystander = await StartUnmanagedAsync($"{_prefix}-bystander");
        var app = Managed($"{_prefix}-app");

        // Act: deploy a bundle, then a desired state with no containers.
        var deployed = await ReconcileAsync(app);
        var emptied = await ReconcileAsync();

        // Assert
        deployed.ContainersStarted.Should().Be(1);
        emptied.ContainersStopped.Should().Be(1);
        (await RunningNamesAsync()).Should().Contain(bystander).And.NotContain(app.Name);
    }

    [Fact]
    public async Task Reconcile_ListsOnlyManagedContainers()
    {
        // Arrange
        var bystander = await StartUnmanagedAsync($"{_prefix}-bystander");
        var app = Managed($"{_prefix}-app");
        await ReconcileAsync(app);

        // Act
        var managed = await _manager.GetManagedContainersAsync();

        // Assert
        managed.Should().Contain(c => c.Name == app.Name && c.IsManaged && c.ManagedImage == Image);
        managed.Should().NotContain(c => c.Name == bystander);
    }

    [Fact]
    public async Task Reconcile_NameHeldByUnmanagedContainer_FailsWithoutRemovingIt()
    {
        // Arrange
        var taken = await StartUnmanagedAsync($"{_prefix}-web");

        // Act
        var result = await ReconcileAsync(Managed(taken));

        // Assert
        result.ContainersFailed.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.Should().Contain("not created by SignalBeam");
        (await RunningNamesAsync()).Should().Contain(taken);
    }

    [Fact]
    public async Task Start_DoesNotRemoveAContainerWhoseNameMerelyContainsTheNewName()
    {
        // Arrange: Docker's name filter would match "{prefix}-webserver" for "{prefix}-web".
        var webserver = await StartUnmanagedAsync($"{_prefix}-webserver");

        // Act
        var result = await ReconcileAsync(Managed($"{_prefix}-web"));

        // Assert
        result.ContainersStarted.Should().Be(1);
        (await RunningNamesAsync()).Should().Contain([webserver, $"{_prefix}-web"]);
    }

    private ContainerSpec Managed(string name)
    {
        _createdNames.Add(name);
        return new ContainerSpec(name, Image);
    }

    private async Task<ReconciliationResult> ReconcileAsync(params ContainerSpec[] containers)
    {
        var result = await _handler.Handle(
            new ReconcileContainersCommand(Guid.NewGuid(), new DesiredState("it-bundle", "1.0.0", containers.ToList())),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    private async Task<string> StartUnmanagedAsync(string name)
    {
        var container = new ContainerBuilder(Image)
            .WithName(name)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(80))
            .Build();
        _unmanaged.Add(container);
        await container.StartAsync();
        return name;
    }

    private async Task<List<string>> RunningNamesAsync() =>
        (await _manager.GetRunningContainersAsync()).Select(c => c.Name).ToList();
}
