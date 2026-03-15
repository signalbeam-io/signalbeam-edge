using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Services;

/// <summary>
/// Background service that listens for bundle assignment push notifications
/// and triggers immediate reconciliation when an assignment is received.
/// </summary>
public class NatsAssignmentListenerService : BackgroundService
{
    private readonly IAssignmentListener _listener;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NatsAssignmentListenerService> _logger;
    private readonly DeviceStateManager _stateManager;

    public NatsAssignmentListenerService(
        IAssignmentListener listener,
        IMessageBus messageBus,
        ILogger<NatsAssignmentListenerService> logger,
        DeviceStateManager stateManager)
    {
        _listener = listener;
        _messageBus = messageBus;
        _logger = logger;
        _stateManager = stateManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NatsAssignmentListenerService starting");

        // Wait for device to be registered
        while (!_stateManager.IsRegistered && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for device registration before starting assignment listener");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var deviceId = _stateManager.DeviceId;
        if (!deviceId.HasValue)
        {
            _logger.LogError("Device registered but ID not available, cannot start assignment listener");
            return;
        }

        // Wire up the event handler for immediate reconciliation
        _listener.OnAssignmentReceived += async message =>
        {
            await OnAssignmentReceivedAsync(message, stoppingToken);
        };

        _logger.LogInformation(
            "Starting assignment listener for device {DeviceId}", deviceId.Value);

        // StartAsync blocks until the subscription loop ends
        await _listener.StartAsync(deviceId.Value, stoppingToken);

        _logger.LogInformation("NatsAssignmentListenerService stopped");
    }

    private async Task OnAssignmentReceivedAsync(
        BundleAssignmentMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Assignment received for bundle {BundleId} v{Version}, triggering immediate reconciliation",
            message.BundleId, message.BundleVersion);

        try
        {
            // Fetch the latest desired state from the cloud
            var fetchCommand = new FetchDesiredStateCommand(message.DeviceId);
            var desiredState = await _messageBus.InvokeAsync<DesiredState?>(
                fetchCommand, cancellationToken);

            if (desiredState is null)
            {
                _logger.LogWarning(
                    "No desired state returned after assignment for bundle {BundleId}, skipping reconciliation",
                    message.BundleId);
                return;
            }

            // Reconcile containers immediately
            var reconcileCommand = new ReconcileContainersCommand(message.DeviceId, desiredState);
            var result = await _messageBus.InvokeAsync<Shared.Infrastructure.Results.Result<ReconciliationResult>>(
                reconcileCommand, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Immediate reconciliation completed: {Started} started, {Stopped} stopped, {Failed} failed",
                    result.Value.ContainersStarted,
                    result.Value.ContainersStopped,
                    result.Value.ContainersFailed);
            }
            else
            {
                _logger.LogError(
                    "Immediate reconciliation failed: {Error}", result.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during immediate reconciliation for bundle {BundleId}",
                message.BundleId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NatsAssignmentListenerService stopping...");
        await _listener.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
