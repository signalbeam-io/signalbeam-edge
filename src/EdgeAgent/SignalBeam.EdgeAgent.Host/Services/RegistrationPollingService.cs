using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Host.Configuration;

namespace SignalBeam.EdgeAgent.Host.Services;

/// <summary>
/// Polls the cloud for registration approval after the device has registered but before it
/// holds an API key. The status handler claims the key once approval lands; this service then
/// promotes the device to "registered" so the heartbeat and reconciliation loops can start.
/// It stops as soon as a key is obtained — or the registration is rejected — so an approved
/// device incurs no ongoing polling.
/// </summary>
public class RegistrationPollingService : BackgroundService
{
    private const int MinIntervalSeconds = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly DeviceStateManager _stateManager;
    private readonly AgentOptions _options;
    private readonly ILogger<RegistrationPollingService> _logger;

    public RegistrationPollingService(
        IServiceScopeFactory scopeFactory,
        IDeviceCredentialsStore credentialsStore,
        DeviceStateManager stateManager,
        IOptions<AgentOptions> options,
        ILogger<RegistrationPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _credentialsStore = credentialsStore;
        _stateManager = stateManager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var credentials = await _credentialsStore.LoadCredentialsAsync(stoppingToken);
        if (credentials is null)
        {
            _logger.LogDebug("Device is not registered; registration polling is idle.");
            return;
        }

        // Already have a key — nothing to poll; just make sure the run loop sees us as registered.
        if (!string.IsNullOrEmpty(credentials.ApiKey))
        {
            PromoteToRegistered(credentials);
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(MinIntervalSeconds, _options.RegistrationPollIntervalSeconds));
        _logger.LogInformation(
            "Device {DeviceId} awaiting approval; polling for approval every {Interval}s.",
            credentials.DeviceId, interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        do
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<CheckRegistrationStatusCommandHandler>();
                    await handler.Handle(new CheckRegistrationStatusCommand(), stoppingToken);
                }

                var updated = await _credentialsStore.LoadCredentialsAsync(stoppingToken);

                if (updated is not null && !string.IsNullOrEmpty(updated.ApiKey))
                {
                    PromoteToRegistered(updated);
                    _logger.LogInformation(
                        "Device {DeviceId} approved and API key obtained; stopping registration polling.",
                        updated.DeviceId);
                    return;
                }

                if (updated is not null &&
                    string.Equals(updated.RegistrationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(
                        "Device {DeviceId} registration was rejected; stopping registration polling.",
                        updated.DeviceId);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration poll failed; will retry on next cycle.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void PromoteToRegistered(DeviceCredentials credentials)
    {
        if (_stateManager.IsRegistered)
        {
            return;
        }

        _stateManager.SetRegistrationState(
            credentials.DeviceId,
            credentials.ApiKey!,
            _options.CloudUrl);
    }
}
