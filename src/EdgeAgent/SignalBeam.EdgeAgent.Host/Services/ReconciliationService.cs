using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Configuration;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Services;

public class ReconciliationService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ICloudClient _cloudClient;
    private readonly ILogger<ReconciliationService> _logger;
    private readonly AgentOptions _options;
    private readonly DeviceStateManager _stateManager;
    private readonly Meter _meter;
    private readonly Counter<long> _reconciliationAttemptsCounter;
    private readonly Counter<long> _reconciliationSuccessCounter;
    private readonly Counter<long> _reconciliationFailureCounter;
    private readonly Counter<long> _containersStartedCounter;
    private readonly Counter<long> _containersStoppedCounter;
    private readonly Counter<long> _containersFailedCounter;

    public ReconciliationService(
        IMessageBus messageBus,
        ICloudClient cloudClient,
        ILogger<ReconciliationService> logger,
        IOptions<AgentOptions> options,
        DeviceStateManager stateManager)
    {
        _messageBus = messageBus;
        _cloudClient = cloudClient;
        _logger = logger;
        _options = options.Value;
        _stateManager = stateManager;

        // Initialize OpenTelemetry metrics
        _meter = new Meter("SignalBeam.EdgeAgent.Reconciliation", "1.0.0");

        _reconciliationAttemptsCounter = _meter.CreateCounter<long>(
            "reconciliation.attempts",
            description: "Total number of reconciliation attempts");

        _reconciliationSuccessCounter = _meter.CreateCounter<long>(
            "reconciliation.success",
            description: "Total number of successful reconciliations");

        _reconciliationFailureCounter = _meter.CreateCounter<long>(
            "reconciliation.failure",
            description: "Total number of failed reconciliations");

        _containersStartedCounter = _meter.CreateCounter<long>(
            "containers.started",
            description: "Total number of containers started");

        _containersStoppedCounter = _meter.CreateCounter<long>(
            "containers.stopped",
            description: "Total number of containers stopped");

        _containersFailedCounter = _meter.CreateCounter<long>(
            "containers.failed",
            description: "Total number of container operations that failed");
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

                Application.Services.DesiredState? desiredState = null;
                ReconciliationResult? reconciliationResult = null;
                string reconciliationStatus = "succeeded";
                var reconciliationActions = new List<Application.Services.ReconciliationAction>();
                var reconciliationErrors = new List<string>();

                try
                {
                    // Record reconciliation attempt
                    _reconciliationAttemptsCounter.Add(1);

                    // Fetch desired state
                    var fetchCommand = new FetchDesiredStateCommand(deviceId.Value);
                    desiredState = await _messageBus.InvokeAsync<Application.Services.DesiredState?>(fetchCommand, stoppingToken);

                    if (desiredState == null)
                    {
                        _logger.LogDebug("No desired state available, skipping reconciliation");
                        continue;
                    }

                    // Reconcile containers
                    var reconcileCommand = new ReconcileContainersCommand(deviceId.Value, desiredState);
                    var result = await _messageBus.InvokeAsync<Shared.Infrastructure.Results.Result<ReconciliationResult>>(
                        reconcileCommand,
                        stoppingToken);

                    if (result.IsSuccess)
                    {
                        reconciliationResult = result.Value;

                        // Convert actions from command result to cloud API format
                        reconciliationActions = reconciliationResult.Actions
                            .Select(a => new Application.Services.ReconciliationAction(a.Action, a.Container, a.Image))
                            .ToList();

                        reconciliationErrors = reconciliationResult.Errors;

                        // Record metrics
                        _containersStartedCounter.Add(reconciliationResult.ContainersStarted);
                        _containersStoppedCounter.Add(reconciliationResult.ContainersStopped);
                        _containersFailedCounter.Add(reconciliationResult.ContainersFailed);

                        if (reconciliationResult.ContainersFailed > 0)
                        {
                            reconciliationStatus = "failed";
                            _reconciliationFailureCounter.Add(1);
                            _logger.LogWarning(
                                "Reconciliation completed with failures: {Failed} failed, {Started} started, {Stopped} stopped",
                                reconciliationResult.ContainersFailed,
                                reconciliationResult.ContainersStarted,
                                reconciliationResult.ContainersStopped);
                        }
                        else
                        {
                            _reconciliationSuccessCounter.Add(1);
                            _logger.LogInformation(
                                "Reconciliation completed successfully: {Started} started, {Stopped} stopped",
                                reconciliationResult.ContainersStarted,
                                reconciliationResult.ContainersStopped);
                        }
                    }
                    else
                    {
                        reconciliationStatus = "failed";
                        _reconciliationFailureCounter.Add(1);
                        reconciliationErrors.Add(result.Error?.Message ?? "Unknown error during reconciliation");
                        _logger.LogError("Reconciliation failed: {Error}", result.Error?.Message);
                    }
                }
                catch (Exception ex)
                {
                    reconciliationStatus = "failed";
                    _reconciliationFailureCounter.Add(1);
                    reconciliationErrors.Add($"Exception during reconciliation: {ex.Message}");
                    _logger.LogError(ex, "Error during reconciliation");
                }
                finally
                {
                    // Report reconciliation status to cloud
                    try
                    {
                        var status = new ReconciliationStatus(
                            DeviceId: deviceId.Value,
                            Status: reconciliationStatus,
                            BundleId: desiredState?.BundleId,
                            BundleVersion: desiredState?.BundleVersion,
                            Timestamp: DateTime.UtcNow,
                            Actions: reconciliationActions,
                            Errors: reconciliationErrors
                        );

                        await _cloudClient.ReportReconciliationStatusAsync(status, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to report reconciliation status to cloud");
                        // Don't throw - we don't want to stop the reconciliation loop
                    }
                }
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
