using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Configuration;

namespace SignalBeam.EdgeAgent.Host.Services;

/// <summary>
/// Periodically checks the device API key expiry and rotates it before it lapses, so a
/// long-running device never loses authentication. Waits until the device is registered
/// (holds a key) before doing any work.
/// </summary>
public class KeyLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeviceStateManager _stateManager;
    private readonly AgentOptions _options;
    private readonly ILogger<KeyLifecycleService> _logger;

    public KeyLifecycleService(
        IServiceScopeFactory scopeFactory,
        DeviceStateManager stateManager,
        IOptions<AgentOptions> options,
        ILogger<KeyLifecycleService> logger)
    {
        _scopeFactory = scopeFactory;
        _stateManager = stateManager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(0.1, _options.KeyLifecycleCheckIntervalHours));
        _logger.LogInformation(
            "KeyLifecycleService started; checking API key expiry every {Interval}h (threshold {Threshold}d).",
            interval.TotalHours, _options.KeyRotationThresholdDays);

        // Wait until the device has a key to manage.
        while (!_stateManager.IsRegistered && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var rotationService = scope.ServiceProvider.GetRequiredService<IKeyRotationService>();
                await rotationService.CheckAndRotateAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API key lifecycle check");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("KeyLifecycleService stopped");
    }
}
