using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Services;
using SignalBeam.Shared.Infrastructure.Results;
using Wolverine;

namespace SignalBeam.EdgeAgent.Tests.Unit.Services;

public class NatsAssignmentListenerServiceTests
{
    private readonly IMessageBus _messageBus;
    private readonly IAssignmentListener _listener;
    private readonly NatsAssignmentListenerService _sut;

    public NatsAssignmentListenerServiceTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _listener = Substitute.For<IAssignmentListener>();
        var logger = Substitute.For<ILogger<NatsAssignmentListenerService>>();
        _sut = new NatsAssignmentListenerService(_messageBus, _listener, new DeviceStateManager(), logger);
    }

    private static AssignmentReceivedEventArgs Assignment(Guid deviceId) => new()
    {
        DeviceId = deviceId,
        BundleId = "bundle-1",
        BundleVersion = "1.0.0",
        AssignedAt = DateTimeOffset.UtcNow
    };

    private static DesiredState DesiredState() =>
        new("bundle-1", "1.0.0", new List<ContainerSpec>());

    [Fact]
    public async Task TriggerReconciliationAsync_FetchesDesiredStateAndReconciles_ForTheAssignedDevice()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _messageBus
            .InvokeAsync<DesiredState?>(Arg.Any<FetchDesiredStateCommand>(), Arg.Any<CancellationToken>())
            .Returns(DesiredState());
        _messageBus
            .InvokeAsync<Result<ReconciliationResult>>(Arg.Any<ReconcileContainersCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReconciliationResult>.Success(
                new ReconciliationResult(1, 0, 0, new List<Application.Commands.ReconciliationAction>(), new List<string>())));

        // Act
        await _sut.TriggerReconciliationAsync(Assignment(deviceId), CancellationToken.None);

        // Assert — both commands dispatched immediately for the pushed device
        await _messageBus.Received(1).InvokeAsync<DesiredState?>(
            Arg.Is<FetchDesiredStateCommand>(c => c.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
        await _messageBus.Received(1).InvokeAsync<Result<ReconciliationResult>>(
            Arg.Is<ReconcileContainersCommand>(c => c.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerReconciliationAsync_WhenNoDesiredState_DoesNotReconcile()
    {
        // Arrange
        _messageBus
            .InvokeAsync<DesiredState?>(Arg.Any<FetchDesiredStateCommand>(), Arg.Any<CancellationToken>())
            .Returns((DesiredState?)null);

        // Act
        await _sut.TriggerReconciliationAsync(Assignment(Guid.NewGuid()), CancellationToken.None);

        // Assert — no reconcile dispatched when there is nothing to converge to
        await _messageBus.DidNotReceive().InvokeAsync<Result<ReconciliationResult>>(
            Arg.Any<ReconcileContainersCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerReconciliationAsync_WhenReconcileFails_DoesNotThrow()
    {
        // Arrange
        _messageBus
            .InvokeAsync<DesiredState?>(Arg.Any<FetchDesiredStateCommand>(), Arg.Any<CancellationToken>())
            .Returns(DesiredState());
        _messageBus
            .InvokeAsync<Result<ReconciliationResult>>(Arg.Any<ReconcileContainersCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ReconciliationResult>(Error.Failure("RECONCILE_FAILED", "boom")));

        // Act
        var act = () => _sut.TriggerReconciliationAsync(Assignment(Guid.NewGuid()), CancellationToken.None);

        // Assert — failures are logged, never bubbled up to crash the listener
        await act.Should().NotThrowAsync();
    }
}
