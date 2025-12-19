using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Host.Configuration;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Services;

public class ReconciliationService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ReconciliationService> _logger;
    private readonly AgentOptions _options;
    private readonly DeviceStateManager _stateManager;

    public ReconciliationService(
        IMessageBus messageBus,
        ILogger<ReconciliationService> logger,
        IOptions<AgentOptions> options,
        DeviceStateManager stateManager)
    {
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
        _stateManager = stateManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReconciliationService starting with {Interval}s interval", _options.ReconciliationIntervalSeconds);

        // Wait for device to be registered
        while (!_stateManager.IsRegistered && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for device registration before starting reconciliation");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("Device registered, starting reconciliation loop");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ReconciliationIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);

                var deviceId = _stateManager.DeviceId;
                if (!deviceId.HasValue)
                {
                    _logger.LogWarning("Device ID not available, skipping reconciliation");
                    continue;
                }

                _logger.LogDebug("Starting reconciliation loop for device {DeviceId}", deviceId.Value);

                // Fetch desired state
                var fetchCommand = new FetchDesiredStateCommand(deviceId.Value);
                var desiredState = await _messageBus.InvokeAsync<Application.Services.DesiredState?>(fetchCommand, stoppingToken);

                if (desiredState == null)
                {
                    _logger.LogDebug("No desired state available, skipping reconciliation");
                    continue;
                }

                // Reconcile containers
                var reconcileCommand = new ReconcileContainersCommand(deviceId.Value, desiredState);
                await _messageBus.InvokeAsync(reconcileCommand, stoppingToken);

                _logger.LogDebug("Reconciliation completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reconciliation");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("ReconciliationService stopped");
    }
}
