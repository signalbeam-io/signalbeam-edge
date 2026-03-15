using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Services;
using SignalBeam.Shared.Infrastructure.Results;
using Wolverine;
using CommandReconciliationAction = SignalBeam.EdgeAgent.Application.Commands.ReconciliationAction;

namespace SignalBeam.EdgeAgent.Tests.Unit.Services;

public class NatsAssignmentListenerServiceTests
{
    private readonly IAssignmentListener _listener;
    private readonly IMessageBus _messageBus;
    private readonly DeviceStateManager _stateManager;
    private readonly NatsAssignmentListenerService _sut;

    // Capture the event handler so we can invoke it in tests
    private Func<BundleAssignmentMessage, Task>? _capturedHandler;

    public NatsAssignmentListenerServiceTests()
    {
        _listener = Substitute.For<IAssignmentListener>();
        _messageBus = Substitute.For<IMessageBus>();
        _stateManager = new DeviceStateManager();
        _stateManager.ClearState(); // Ensure no persisted state from previous runs

        // Capture the OnAssignmentReceived event handler when it's attached
        _listener.When(l => l.OnAssignmentReceived += Arg.Any<Func<BundleAssignmentMessage, Task>>())
            .Do(callInfo =>
            {
                _capturedHandler = callInfo.Arg<Func<BundleAssignmentMessage, Task>>();
            });

        // Make StartAsync return immediately so tests don't block
        _listener.StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var logger = Substitute.For<ILogger<NatsAssignmentListenerService>>();
        _sut = new NatsAssignmentListenerService(_listener, _messageBus, logger, _stateManager);
    }

    [Fact]
    public async Task ExecuteAsync_WaitsForDeviceRegistration()
    {
        // Arrange — device is not registered
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartAsync(cts.Token);
        try { await Task.Delay(250, cts.Token); } catch (OperationCanceledException) { }
        await _sut.StopAsync(CancellationToken.None);

        // Assert — listener should never have been started since device wasn't registered
        await _listener.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StartsListenerWhenDeviceRegistered()
    {
        // Arrange — register device
        var deviceId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100); // Give ExecuteAsync time to run
        await _sut.StopAsync(CancellationToken.None);

        // Assert — listener should be started with the correct device ID
        await _listener.Received(1).StartAsync(deviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAssignmentReceived_DispatchesFetchDesiredStateCommand()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        var desiredState = new DesiredState(Guid.NewGuid().ToString(), "1.0.0", new List<ContainerSpec>());

        _messageBus.InvokeAsync<DesiredState?>(
            Arg.Any<FetchDesiredStateCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(desiredState);

        _messageBus.InvokeAsync<Result<ReconciliationResult>>(
            Arg.Any<ReconcileContainersCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<ReconciliationResult>.Success(
                new ReconciliationResult(1, 0, 0, new List<CommandReconciliationAction>(), new List<string>())));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act — simulate assignment received
        _capturedHandler.Should().NotBeNull("handler should be attached before StartAsync");
        var message = new BundleAssignmentMessage(deviceId, Guid.NewGuid(), "2.0.0", DateTimeOffset.UtcNow);
        await _capturedHandler!(message);

        await _sut.StopAsync(CancellationToken.None);

        // Assert
        await _messageBus.Received(1).InvokeAsync<DesiredState?>(
            Arg.Is<FetchDesiredStateCommand>(cmd => cmd.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAssignmentReceived_DispatchesReconcileContainersCommand()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        var desiredState = new DesiredState(bundleId.ToString(), "2.0.0", new List<ContainerSpec>
        {
            new("app", "myimage:2.0.0")
        });

        _messageBus.InvokeAsync<DesiredState?>(
            Arg.Any<FetchDesiredStateCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(desiredState);

        _messageBus.InvokeAsync<Result<ReconciliationResult>>(
            Arg.Any<ReconcileContainersCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<ReconciliationResult>.Success(
                new ReconciliationResult(1, 0, 0, new List<CommandReconciliationAction>(), new List<string>())));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act
        var message = new BundleAssignmentMessage(deviceId, bundleId, "2.0.0", DateTimeOffset.UtcNow);
        await _capturedHandler!(message);

        await _sut.StopAsync(CancellationToken.None);

        // Assert
        await _messageBus.Received(1).InvokeAsync<Result<ReconciliationResult>>(
            Arg.Is<ReconcileContainersCommand>(cmd =>
                cmd.DeviceId == deviceId && cmd.DesiredState == desiredState),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAssignmentReceived_SkipsReconciliation_WhenNoDesiredState()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        _messageBus.InvokeAsync<DesiredState?>(
            Arg.Any<FetchDesiredStateCommand>(),
            Arg.Any<CancellationToken>())
            .Returns((DesiredState?)null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act
        var message = new BundleAssignmentMessage(deviceId, Guid.NewGuid(), "1.0.0", DateTimeOffset.UtcNow);
        await _capturedHandler!(message);

        await _sut.StopAsync(CancellationToken.None);

        // Assert — reconcile command should not have been invoked
        await _messageBus.DidNotReceive().InvokeAsync<Result<ReconciliationResult>>(
            Arg.Any<ReconcileContainersCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAssignmentReceived_HandlesReconciliationFailureGracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        var desiredState = new DesiredState(Guid.NewGuid().ToString(), "1.0.0", new List<ContainerSpec>());

        _messageBus.InvokeAsync<DesiredState?>(
            Arg.Any<FetchDesiredStateCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(desiredState);

        _messageBus.InvokeAsync<Result<ReconciliationResult>>(
            Arg.Any<ReconcileContainersCommand>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ReconciliationResult>(
                Error.Failure("Reconciliation.Failed", "Container pull failed")));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act — should not throw
        var message = new BundleAssignmentMessage(deviceId, Guid.NewGuid(), "1.0.0", DateTimeOffset.UtcNow);
        var act = () => _capturedHandler!(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_StopsListener()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _stateManager.SetRegistrationState(deviceId, "test-api-key", "https://api.test.com");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        // Act
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        await _listener.Received(1).StopAsync();
    }
}
