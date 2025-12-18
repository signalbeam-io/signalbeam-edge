using System.Text.Json;
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
    private readonly ILogger<NatsConsumerService> _logger;
    private readonly NatsConnection _connection;
    private readonly INatsJSContext _jetStreamContext;
    private readonly NatsOptions _natsOptions;
    private readonly DeviceHeartbeatMessageHandler _heartbeatHandler;
    private readonly DeviceMetricsMessageHandler _metricsHandler;

    public NatsConsumerService(
        ILogger<NatsConsumerService> logger,
        NatsConnection connection,
        INatsJSContext jetStreamContext,
        IOptions<NatsOptions> natsOptions,
        DeviceHeartbeatMessageHandler heartbeatHandler,
        DeviceMetricsMessageHandler metricsHandler)
    {
        _logger = logger;
        _connection = connection;
        _jetStreamContext = jetStreamContext;
        _natsOptions = natsOptions.Value;
        _heartbeatHandler = heartbeatHandler;
        _metricsHandler = metricsHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NATS Consumer Service starting...");

        try
        {
            // Ensure streams exist
            await EnsureStreamsExistAsync(stoppingToken);

            // Start consuming device metrics
            var metricsTask = ConsumeDeviceMetricsAsync(stoppingToken);

            // Start consuming device heartbeats
            var heartbeatsTask = ConsumeDeviceHeartbeatsAsync(stoppingToken);

            // Wait for both consumers to complete (which should be never unless cancellation is requested)
            await Task.WhenAll(metricsTask, heartbeatsTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NATS Consumer Service stopping due to cancellation...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in NATS Consumer Service");
            throw;
        }
    }

    private async Task EnsureStreamsExistAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring NATS JetStream streams exist...");

        // Ensure DEVICE_METRICS stream exists
        try
        {
            var metricsStream = await _jetStreamContext.GetStreamAsync(
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
            var heartbeatsStream = await _jetStreamContext.GetStreamAsync(
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

    private async Task ConsumeDeviceMetricsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Device Metrics consumer...");

        var consumer = await _jetStreamContext.CreateOrUpdateConsumerAsync(
            _natsOptions.Streams.DeviceMetrics,
            new ConsumerConfig
            {
                Name = "telemetry-processor-metrics",
                DurableName = "telemetry-processor-metrics",
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = TimeSpan.FromSeconds(30),
                MaxDeliver = 3,
                FilterSubject = _natsOptions.Subjects.DeviceMetrics
            },
            cancellationToken);

        _logger.LogInformation("Device Metrics consumer created, starting message processing...");

        // Consume messages in a loop
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Fetch and process messages
                await foreach (var msg in consumer.FetchAsync<byte[]>(
                    new NatsJSFetchOpts { MaxMsgs = 10, Expires = TimeSpan.FromSeconds(5) },
                    serializer: default,
                    cancellationToken))
                {
                    try
                    {
                        // Deserialize message
                        var message = JsonSerializer.Deserialize<DeviceMetricsMessage>(msg.Data);
                        if (message == null)
                        {
                            _logger.LogWarning("Received null metrics message, skipping");
                            await msg.AckAsync(cancellationToken: cancellationToken);
                            continue;
                        }

                        // Process message using Application layer handler
                        await _metricsHandler.Handle(message, cancellationToken);

                        // Acknowledge successful processing
                        await msg.AckAsync(cancellationToken: cancellationToken);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize metrics message");
                        await msg.AckAsync(cancellationToken: cancellationToken); // Ack to skip bad message
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing metrics message");
                        await msg.NakAsync(delay: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Device Metrics consumer cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Device Metrics consumer loop, retrying...");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ConsumeDeviceHeartbeatsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Device Heartbeats consumer...");

        var consumer = await _jetStreamContext.CreateOrUpdateConsumerAsync(
            _natsOptions.Streams.DeviceHeartbeats,
            new ConsumerConfig
            {
                Name = "telemetry-processor-heartbeats",
                DurableName = "telemetry-processor-heartbeats",
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = TimeSpan.FromSeconds(30),
                MaxDeliver = 3,
                FilterSubject = _natsOptions.Subjects.DeviceHeartbeats
            },
            cancellationToken);

        _logger.LogInformation("Device Heartbeats consumer created, starting message processing...");

        // Consume messages in a loop
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Fetch and process messages
                await foreach (var msg in consumer.FetchAsync<byte[]>(
                    new NatsJSFetchOpts { MaxMsgs = 10, Expires = TimeSpan.FromSeconds(5) },
                    serializer: default,
                    cancellationToken))
                {
                    try
                    {
                        // Deserialize message
                        var message = JsonSerializer.Deserialize<DeviceHeartbeatMessage>(msg.Data);
                        if (message == null)
                        {
                            _logger.LogWarning("Received null heartbeat message, skipping");
                            await msg.AckAsync(cancellationToken: cancellationToken);
                            continue;
                        }

                        // Process message using Application layer handler
                        await _heartbeatHandler.Handle(message, cancellationToken);

                        // Acknowledge successful processing
                        await msg.AckAsync(cancellationToken: cancellationToken);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize heartbeat message");
                        await msg.AckAsync(cancellationToken: cancellationToken); // Ack to skip bad message
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing heartbeat message");
                        await msg.NakAsync(delay: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Device Heartbeats consumer cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Device Heartbeats consumer loop, retrying...");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
