using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using SignalBeam.DeviceManager.Application.Commands;

namespace SignalBeam.DeviceManager.Infrastructure.BackgroundServices;

public sealed class StaleDeviceSubscriberOptions
{
    public const string SectionName = "DeviceStatus:StaleEvents";

    public bool Enabled { get; set; } = true;

    public string Subject { get; set; } = "signalbeam.devices.events.stale_detected";

    // With several DeviceManager replicas, only one handles each event.
    public string QueueGroup { get; set; } = "device-manager";
}

// Marks devices offline when TelemetryProcessor's DeviceStatusMonitor reports them stale.
// Resubscribes with backoff so DeviceManager survives NATS being down at startup.
public sealed class StaleDeviceSubscriber : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly INatsConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleDeviceSubscriber> _logger;
    private readonly string _subject;
    private readonly string _queueGroup;

    public StaleDeviceSubscriber(
        INatsConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<StaleDeviceSubscriber> logger,
        Microsoft.Extensions.Options.IOptions<StaleDeviceSubscriberOptions> options)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _subject = options.Value.Subject;
        _queueGroup = options.Value.QueueGroup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = InitialRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Subscribing to stale device events on {Subject}", _subject);
                await foreach (var msg in _connection.SubscribeAsync<string>(_subject, _queueGroup, cancellationToken: stoppingToken))
                {
                    delay = InitialRetryDelay;
                    await HandleAsync(msg, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Stale device subscription failed (NATS may be unavailable); resubscribing in {DelaySeconds}s",
                    delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = delay * 2 > MaxRetryDelay ? MaxRetryDelay : delay * 2;
            }
        }
    }

    private async Task HandleAsync(NatsMsg<string> msg, CancellationToken cancellationToken)
    {
        // Continues the trace of TelemetryProcessor's stale check.
        using var activity = msg.StartActivity(
            $"process {_subject}",
            [new KeyValuePair<string, object?>("messaging.operation.type", "process")]);

        await ProcessAsync(msg.Data, cancellationToken);
    }

    // Returns true when the event reached MarkDeviceOfflineHandler.
    public async Task<bool> ProcessAsync(string? data, CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        try
        {
            var command = data is null ? null : JsonSerializer.Deserialize<MarkDeviceOfflineCommand>(data, JsonOptions);
            if (command is null || command.DeviceId == Guid.Empty)
            {
                _logger.LogWarning("Ignoring stale device event without a device id");
                return false;
            }

            activity?.SetTag("signalbeam.device.id", command.DeviceId.ToString());

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<MarkDeviceOfflineHandler>();
            var result = await handler.Handle(command, cancellationToken);

            if (result.IsFailure)
            {
                // Unknown devices are expected (e.g. pruned pending devices); not an error.
                _logger.LogDebug("Stale device event for {DeviceId} not applied: {Error}", command.DeviceId, result.Error?.Code);
            }
            else if (result.Value!.MarkedOffline)
            {
                _logger.LogInformation("Marked device {DeviceId} offline (stale since {StaleSince})",
                    command.DeviceId, command.LastHeartbeatTime);
            }

            return true;
        }
        catch (JsonException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "deserialization failed");
            activity?.AddException(ex);
            _logger.LogWarning(ex, "Failed to deserialize stale device event");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to process stale device event");
            return false;
        }
    }
}
