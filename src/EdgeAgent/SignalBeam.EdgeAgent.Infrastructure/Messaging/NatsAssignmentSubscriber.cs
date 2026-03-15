using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Messaging;

/// <summary>
/// Subscribes to bundle assignment push notifications via NATS JetStream.
/// Uses a durable consumer to ensure at-least-once delivery of assignment messages.
/// </summary>
public sealed class NatsAssignmentSubscriber : IAssignmentListener
{
    private readonly INatsJSContext _jetStream;
    private readonly ILogger<NatsAssignmentSubscriber> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _internalCts;

    public NatsAssignmentSubscriber(
        INatsJSContext jetStream,
        ILogger<NatsAssignmentSubscriber> logger)
    {
        _jetStream = jetStream;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public event Func<BundleAssignmentMessage, Task>? OnAssignmentReceived;

    public async Task StartAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(deviceId, Guid.Empty);

        var subject = $"signalbeam.bundles.assignments.{deviceId}";
        var streamName = "BUNDLE_ASSIGNMENTS";
        var consumerName = $"edge-agent-{deviceId}";

        _logger.LogInformation(
            "Starting assignment subscriber for device {DeviceId} on subject {Subject}",
            deviceId, subject);

        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _internalCts.Token;

        // Ensure stream exists (may already be created by the cloud side)
        await EnsureStreamExistsAsync(streamName, subject, token);

        // Create a durable consumer for this device
        var consumer = await _jetStream.CreateOrUpdateConsumerAsync(
            streamName,
            new ConsumerConfig
            {
                Name = consumerName,
                DurableName = consumerName,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = TimeSpan.FromSeconds(30),
                MaxDeliver = 5,
                FilterSubject = subject
            },
            token);

        _logger.LogInformation(
            "Assignment consumer {ConsumerName} created, listening for assignments",
            consumerName);

        // Consume messages in a loop
        while (!token.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in consumer.FetchAsync<byte[]>(
                    new NatsJSFetchOpts { MaxMsgs = 1, Expires = TimeSpan.FromSeconds(5) },
                    serializer: default,
                    cancellationToken: token))
                {
                    await ProcessMessageAsync(msg, token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in assignment consumer loop, retrying...");
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }

        _logger.LogInformation("Assignment subscriber stopped for device {DeviceId}", deviceId);
    }

    public Task StopAsync()
    {
        var cts = Interlocked.Exchange(ref _internalCts, null);
        cts?.Cancel();
        cts?.Dispose();
        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(INatsJSMsg<byte[]> msg, CancellationToken cancellationToken)
    {
        try
        {
            if (msg.Data is null or { Length: 0 })
            {
                _logger.LogWarning("Received empty assignment message payload, skipping");
                await msg.AckAsync(cancellationToken: cancellationToken);
                return;
            }

            var message = JsonSerializer.Deserialize<BundleAssignmentMessage>(msg.Data, _jsonOptions);
            if (message is null)
            {
                _logger.LogWarning("Received null assignment message, skipping");
                await msg.AckAsync(cancellationToken: cancellationToken);
                return;
            }

            _logger.LogInformation(
                "Received bundle assignment: DeviceId={DeviceId}, BundleId={BundleId}, Version={Version}",
                message.DeviceId, message.BundleId, message.BundleVersion);

            if (OnAssignmentReceived is not null)
            {
                await OnAssignmentReceived.Invoke(message);
            }

            await msg.AckAsync(cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize assignment message");
            await msg.AckAsync(cancellationToken: cancellationToken); // Ack to skip bad message
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing assignment message");
            await msg.NakAsync(delay: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureStreamExistsAsync(
        string streamName, string subject, CancellationToken cancellationToken)
    {
        try
        {
            await _jetStream.GetStreamAsync(streamName, cancellationToken: cancellationToken);
            _logger.LogDebug("Stream {StreamName} already exists", streamName);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            _logger.LogInformation("Creating stream {StreamName}...", streamName);

            // Use wildcard subject so all device assignments go to the same stream
            var config = new StreamConfig(
                streamName,
                new[] { "signalbeam.bundles.assignments.>" })
            {
                Retention = StreamConfigRetention.Limits,
                MaxAge = TimeSpan.FromDays(7),
                Storage = StreamConfigStorage.File
            };

            await _jetStream.CreateStreamAsync(config, cancellationToken);
            _logger.LogInformation("Stream {StreamName} created", streamName);
        }
    }
}
