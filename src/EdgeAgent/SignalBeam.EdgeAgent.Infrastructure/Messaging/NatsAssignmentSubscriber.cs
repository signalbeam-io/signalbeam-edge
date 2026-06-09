using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Messaging;

/// <summary>
/// Subscribes to bundle assignment push notifications on
/// <c>signalbeam.bundles.assignments.{deviceId}</c> via Core NATS and raises
/// <see cref="OnAssignmentReceived"/> so the host can reconcile immediately
/// instead of waiting for the next periodic poll.
/// </summary>
/// <remarks>
/// Uses Core NATS (at-most-once) to mirror the cloud-side publish, which goes
/// through the shared <c>IMessagePublisher</c> / <c>NatsMessagePublisher</c>
/// (also Core NATS). The periodic reconciliation loop is the safety net for any
/// missed push, so at-most-once delivery is acceptable for this slice; JetStream
/// (durable, at-least-once with ack) can be layered on later without changing
/// the <see cref="IAssignmentListener"/> contract.
/// </remarks>
public sealed class NatsAssignmentSubscriber : IAssignmentListener, IAsyncDisposable
{
    private readonly INatsConnection _connection;
    private readonly ILogger<NatsAssignmentSubscriber> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private CancellationTokenSource? _cts;
    private Task? _subscriptionTask;

    public event EventHandler<AssignmentReceivedEventArgs>? OnAssignmentReceived;

    public NatsAssignmentSubscriber(
        INatsConnection connection,
        ILogger<NatsAssignmentSubscriber> logger)
    {
        _connection = connection;
        _logger = logger;

        // Mirror NatsMessagePublisher's camelCase policy so the wire payload
        // published by BundleOrchestrator deserializes back into PascalCase records.
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task StartListeningAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        if (_subscriptionTask is not null)
        {
            _logger.LogWarning("Assignment listener already started; ignoring StartListeningAsync");
            return Task.CompletedTask;
        }

        var subject = $"signalbeam.bundles.assignments.{deviceId}";
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Run the subscription loop in the background so callers (the host's
        // BackgroundService) are not blocked — messages arrive via the event.
        _subscriptionTask = Task.Run(() => SubscribeLoopAsync(subject, _cts.Token), CancellationToken.None);

        _logger.LogInformation("Started listening for bundle assignments on {Subject}", subject);
        return Task.CompletedTask;
    }

    private async Task SubscribeLoopAsync(string subject, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var msg in _connection.SubscribeAsync<byte[]>(subject, cancellationToken: cancellationToken))
            {
                HandlePayload(subject, msg.Data);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on StopListeningAsync / host shutdown.
            _logger.LogDebug("Assignment subscription on {Subject} cancelled", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assignment subscription loop on {Subject} terminated unexpectedly", subject);
        }
    }

    /// <summary>
    /// Deserializes a raw assignment payload and raises <see cref="OnAssignmentReceived"/>.
    /// Extracted from the subscription loop so it can be unit tested without a live
    /// NATS connection. Malformed payloads are logged and dropped, never thrown.
    /// </summary>
    internal void HandlePayload(string subject, byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            _logger.LogWarning("Received empty assignment message on {Subject}", subject);
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize<BundleAssignmentMessage>(data, _jsonOptions);
            if (message is null)
            {
                _logger.LogWarning("Failed to deserialize assignment message on {Subject}", subject);
                return;
            }

            _logger.LogInformation(
                "Received bundle assignment for device {DeviceId}: bundle {BundleId} version {BundleVersion}",
                message.DeviceId, message.BundleId, message.BundleVersion);

            OnAssignmentReceived?.Invoke(this, new AssignmentReceivedEventArgs
            {
                DeviceId = message.DeviceId,
                BundleId = message.BundleId,
                // BundleVersion is optional on the wire (the domain event does
                // not yet carry it); default to empty rather than throwing.
                BundleVersion = message.BundleVersion ?? string.Empty,
                AssignedAt = message.AssignedAt
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid assignment message JSON on {Subject}", subject);
        }
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync();

        if (_subscriptionTask is not null)
        {
            try
            {
                await _subscriptionTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The loop was cancelled or the wait timed out — either way we are stopping.
            }
        }

        _cts.Dispose();
        _cts = null;
        _subscriptionTask = null;

        _logger.LogInformation("Stopped listening for bundle assignments");
    }

    public async ValueTask DisposeAsync()
    {
        await StopListeningAsync();
    }
}

/// <summary>
/// Wire schema for bundle assignment push messages on
/// <c>signalbeam.bundles.assignments.{deviceId}</c>. Mirrors the payload
/// published by BundleOrchestrator's <c>BundleAssignedEventHandler</c> (#338).
/// <c>BundleVersion</c> is nullable because the originating domain event does
/// not currently carry a version.
/// </summary>
internal sealed record BundleAssignmentMessage(
    Guid DeviceId,
    string BundleId,
    string? BundleVersion,
    DateTimeOffset AssignedAt);
