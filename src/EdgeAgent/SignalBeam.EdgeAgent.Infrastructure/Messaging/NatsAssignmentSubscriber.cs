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

    private readonly object _stateLock = new();
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
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Bound nesting depth so an adversarial payload can't exhaust the stack.
            MaxDepth = 32
        };
    }

    public Task StartListeningAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var subject = $"signalbeam.bundles.assignments.{deviceId}";

        // Guard check-and-assign under a lock: the listener is a singleton on a
        // public interface, so two concurrent StartListeningAsync calls must not
        // both spin up a subscription loop on the same subject.
        lock (_stateLock)
        {
            if (_subscriptionTask is not null)
            {
                _logger.LogWarning("Assignment listener already started; ignoring StartListeningAsync");
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Run the subscription loop in the background so callers (the host's
            // BackgroundService) are not blocked — messages arrive via the event.
            _subscriptionTask = Task.Run(() => SubscribeLoopAsync(subject, _cts.Token), CancellationToken.None);
        }

        _logger.LogInformation("Started listening for bundle assignments on {Subject}", subject);
        return Task.CompletedTask;
    }

    private async Task SubscribeLoopAsync(string subject, CancellationToken cancellationToken)
    {
        try
        {
            // Subscribe as byte[] and JSON-deserialize manually: the cloud side
            // publishes via the shared NatsMessagePublisher, which writes UTF-8 JSON
            // to the wire, so the raw bytes are exactly that JSON. This mirrors the
            // established NatsSseBridgeService consumer pattern.
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
        CancellationTokenSource? cts;
        Task? subscriptionTask;

        // Snapshot and clear the fields under the lock so a concurrent Start can't
        // swap them out between cancel and dispose (which would dispose the wrong CTS).
        lock (_stateLock)
        {
            cts = _cts;
            subscriptionTask = _subscriptionTask;
            _cts = null;
            _subscriptionTask = null;
        }

        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync();

        if (subscriptionTask is not null)
        {
            try
            {
                // Bound the drain so a wedged loop (e.g. dead NATS connection) cannot
                // stall host shutdown indefinitely.
                await subscriptionTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The loop was cancelled or the caller's token fired — we are stopping.
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Assignment subscription did not drain within timeout during shutdown");
            }
        }

        cts.Dispose();

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
