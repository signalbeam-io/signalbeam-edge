using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Host.Configuration;
using Wolverine;

namespace SignalBeam.EdgeAgent.Host.Services;

public class HeartbeatService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly AgentOptions _options;
    private readonly DeviceStateManager _stateManager;

    public HeartbeatService(
        IMessageBus messageBus,
        ILogger<HeartbeatService> logger,
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
        _logger.LogInformation("HeartbeatService starting with {Interval}s interval", _options.HeartbeatIntervalSeconds);

        // Wait for device to be registered
        while (!_stateManager.IsRegistered && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for device registration before starting heartbeat");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("Device registered, starting heartbeat loop");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);

                var deviceId = _stateManager.DeviceId;
                if (!deviceId.HasValue)
                {
                    _logger.LogWarning("Device ID not available, skipping heartbeat");
                    continue;
                }

                _logger.LogDebug("Sending heartbeat for device {DeviceId}", deviceId.Value);

                var command = new SendHeartbeatCommand(deviceId.Value);
                var result = await _messageBus.InvokeAsync<SendHeartbeatCommand>(command, stoppingToken);

                if (result != null)
                {
                    _logger.LogDebug("Heartbeat sent successfully");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("HeartbeatService stopped");
    }
}
