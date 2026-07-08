using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging.Options;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Messaging;

/// <summary>
/// Background service that consumes telemetry messages from NATS JetStream.
/// Processes device heartbeats and metrics from the message broker.
/// </summary>
public class NatsConsumerService : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

    private readonly ILogger<NatsConsumerService> _logger;
    private readonly NatsConnection _connection;
    private readonly INatsJSContext _jetStreamContext;
    private readonly NatsOptions _natsOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public NatsConsumerService(
        ILogger<NatsConsumerService> logger,
        NatsConnection connection,
        INatsJSContext jetStreamContext,
        IOptions<NatsOptions> natsOptions,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _connection = connection;
        _jetStreamContext = jetStreamContext;
        _natsOptions = natsOptions.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NATS Consumer Service starting...");

        try
        {
            // Retry until NATS is reachable — the service must survive a broker
            // outage at startup and recover without a redeploy (#387).
            var delay = InitialRetryDelay;
            while (true)
            {
                try
                {
                    await EnsureStreamsExistAsync(stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to ensure JetStream streams (NATS may be unavailable); retrying in {DelaySeconds}s",
                        delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);
                    delay = Grow(delay);
                }
            }

            // Each consumer is self-healing; these only complete on cancellation.
            var metricsTask = ConsumeDeviceMetricsAsync(stoppingToken);
            var heartbeatsTask = ConsumeDeviceHeartbeatsAsync(stoppingToken);
            await Task.WhenAll(metricsTask, heartbeatsTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NATS Consumer Service stopping due to cancellation...");
        }
    }

    private static TimeSpan Grow(TimeSpan delay) =>
        delay * 2 > MaxRetryDelay ? MaxRetryDelay : delay * 2;

    private async Task EnsureStreamsExistAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring NATS JetStream streams exist...");

        // Ensure DEVICE_METRICS stream exists
        try
        {
            _ = await _jetStreamContext.GetStreamAsync(
                _natsOptions.Streams.DeviceMetrics);

            _logger.LogInformation("Stream {StreamName} already exists", _natsOptions.Streams.DeviceMetrics);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            _logger.LogInformation("Creating stream {StreamName}...", _natsOptions.Streams.DeviceMetrics);

            var config = new StreamConfig(
                _natsOptions.Streams.DeviceMetrics,
                new[] { _natsOptions.Subjects.DeviceMetrics })
            {
                Retention = StreamConfigRetention.Limits,
                MaxAge = TimeSpan.FromDays(30), // Keep messages for 30 days
                Storage = StreamConfigStorage.File
            };

            await _jetStreamContext.CreateStreamAsync(config, cancellationToken);
            _logger.LogInformation("Stream {StreamName} created", _natsOptions.Streams.DeviceMetrics);
        }

        // Ensure DEVICE_HEARTBEATS stream exists
        try
        {
            _ = await _jetStreamContext.GetStreamAsync(
                _natsOptions.Streams.DeviceHeartbeats);

            _logger.LogInformation("Stream {StreamName} already exists", _natsOptions.Streams.DeviceHeartbeats);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            _logger.LogInformation("Creating stream {StreamName}...", _natsOptions.Streams.DeviceHeartbeats);

            var config = new StreamConfig(
                _natsOptions.Streams.DeviceHeartbeats,
                new[] { _natsOptions.Subjects.DeviceHeartbeats })
            {
                Retention = StreamConfigRetention.Limits,
                MaxAge = TimeSpan.FromDays(30),
                Storage = StreamConfigStorage.File
            };

            await _jetStreamContext.CreateStreamAsync(config, cancellationToken);
            _logger.LogInformation("Stream {StreamName} created", _natsOptions.Streams.DeviceHeartbeats);
        }
    }

    private Task ConsumeDeviceMetricsAsync(CancellationToken cancellationToken) =>
        ConsumeLoopAsync<DeviceMetricsMessage>(
            _natsOptions.Streams.DeviceMetrics,
            "telemetry-processor-metrics",
            _natsOptions.Subjects.DeviceMetrics,
            "Device Metrics",
            (scope, message, token) => scope.ServiceProvider
                .GetRequiredService<DeviceMetricsMessageHandler>()
                .Handle(message, token),
            cancellationToken);

    private Task ConsumeDeviceHeartbeatsAsync(CancellationToken cancellationToken) =>
        ConsumeLoopAsync<DeviceHeartbeatMessage>(
            _natsOptions.Streams.DeviceHeartbeats,
            "telemetry-processor-heartbeats",
            _natsOptions.Subjects.DeviceHeartbeats,
            "Device Heartbeats",
            (scope, message, token) => scope.ServiceProvider
                .GetRequiredService<DeviceHeartbeatMessageHandler>()
                .Handle(message, token),
            cancellationToken);

    /// <summary>
    /// Self-healing consume loop: the JetStream consumer is (re)created inside the
    /// retry loop so a broker outage recovers without a redeploy (#387).
    /// </summary>
    private async Task ConsumeLoopAsync<TMessage>(
        string streamName,
        string consumerName,
        string filterSubject,
        string description,
        Func<IServiceScope, TMessage, CancellationToken, Task> handleAsync,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        _logger.LogInformation("Starting {Description} consumer...", description);

        var delay = InitialRetryDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumer = await _jetStreamContext.CreateOrUpdateConsumerAsync(
                    streamName,
                    new ConsumerConfig
                    {
                        Name = consumerName,
                        DurableName = consumerName,
                        AckPolicy = ConsumerConfigAckPolicy.Explicit,
                        AckWait = TimeSpan.FromSeconds(30),
                        MaxDeliver = 3,
                        FilterSubject = filterSubject
                    },
                    cancellationToken);

                _logger.LogInformation("{Description} consumer created, starting message processing...", description);
                delay = InitialRetryDelay;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Fetch and process messages
                    await foreach (var msg in consumer.FetchAsync<byte[]>(
                        new NatsJSFetchOpts { MaxMsgs = 10, Expires = TimeSpan.FromSeconds(5) },
                        serializer: default,
                        cancellationToken))
                    {
                        try
                        {
                            var message = JsonSerializer.Deserialize<TMessage>(msg.Data);
                            if (message == null)
                            {
                                _logger.LogWarning("Received null {Description} message, skipping", description);
                                await msg.AckAsync(cancellationToken: cancellationToken);
                                continue;
                            }

                            // Create scope to resolve scoped handler
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                await handleAsync(scope, message, cancellationToken);
                            }

                            // Acknowledge successful processing
                            await msg.AckAsync(cancellationToken: cancellationToken);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Failed to deserialize {Description} message", description);
                            await msg.AckAsync(cancellationToken: cancellationToken); // Ack to skip bad message
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing {Description} message", description);
                            await msg.NakAsync(delay: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{Description} consumer cancelled", description);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "{Description} consumer failed (NATS may be unavailable); recreating consumer in {DelaySeconds}s",
                    description, delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                delay = Grow(delay);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NATS Consumer Service stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("NATS Consumer Service stopped");
    }
}
