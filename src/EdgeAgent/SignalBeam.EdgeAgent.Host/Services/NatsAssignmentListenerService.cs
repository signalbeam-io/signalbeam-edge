using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Services;

/// <summary>
/// Background service that wraps <see cref="IAssignmentListener"/> and triggers
/// an immediate reconciliation when a bundle assignment is pushed from the cloud
/// via NATS — closing the gap between assignment and convergence that the
/// periodic <see cref="ReconciliationService"/> loop would otherwise leave.
/// </summary>
public sealed class NatsAssignmentListenerService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly IAssignmentListener _listener;
    private readonly DeviceStateManager _stateManager;
    private readonly ILogger<NatsAssignmentListenerService> _logger;

    private CancellationToken _stoppingToken = CancellationToken.None;

    public NatsAssignmentListenerService(
        IMessageBus messageBus,
        IAssignmentListener listener,
        DeviceStateManager stateManager,
        ILogger<NatsAssignmentListenerService> logger)
    {
        _messageBus = messageBus;
        _listener = listener;
        _stateManager = stateManager;
        _logger = logger;

        // Wire the handler in the constructor so the subscription is testable
        // independently of the registration-wait in ExecuteAsync.
        _listener.OnAssignmentReceived += OnAssignmentReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        // Wait for the device to be registered before we know our subject.
        while (!_stateManager.IsRegistered && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for device registration before listening for assignments");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var deviceId = _stateManager.DeviceId;
        if (!deviceId.HasValue)
        {
            _logger.LogWarning("Device registered but ID unavailable; assignment listener not started");
            return;
        }

        try
        {
            await _listener.StartListeningAsync(deviceId.Value, stoppingToken);

            // Stay alive until shutdown; messages are delivered via the event handler.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _listener.OnAssignmentReceived -= OnAssignmentReceived;
            await _listener.StopListeningAsync(CancellationToken.None);
        }
    }

    private void OnAssignmentReceived(object? sender, AssignmentReceivedEventArgs e)
    {
        // Event handlers are synchronous; dispatch the reconciliation without
        // blocking the NATS subscription loop. Exceptions are handled inside.
        _ = TriggerReconciliationAsync(e, _stoppingToken);
    }

    /// <summary>
    /// Fetches the latest desired state and reconciles containers immediately in
    /// response to an assignment push. Internal for unit testing.
    /// </summary>
    internal async Task TriggerReconciliationAsync(
        AssignmentReceivedEventArgs assignment,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Assignment push received for device {DeviceId} (bundle {BundleId}); reconciling immediately",
                assignment.DeviceId, assignment.BundleId);

            var desiredState = await _messageBus.InvokeAsync<DesiredState?>(
                new FetchDesiredStateCommand(assignment.DeviceId),
                cancellationToken);

            if (desiredState is null)
            {
                _logger.LogWarning(
                    "No desired state returned for device {DeviceId} after assignment push; skipping reconcile",
                    assignment.DeviceId);
                return;
            }

            var result = await _messageBus.InvokeAsync<Result<ReconciliationResult>>(
                new ReconcileContainersCommand(assignment.DeviceId, desiredState),
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Push-triggered reconciliation completed for device {DeviceId}: {Started} started, {Stopped} stopped, {Failed} failed",
                    assignment.DeviceId,
                    result.Value.ContainersStarted,
                    result.Value.ContainersStopped,
                    result.Value.ContainersFailed);
            }
            else
            {
                _logger.LogError(
                    "Push-triggered reconciliation failed for device {DeviceId}: {Error}",
                    assignment.DeviceId, result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during push-triggered reconciliation for device {DeviceId}", assignment.DeviceId);
        }
    }
}
