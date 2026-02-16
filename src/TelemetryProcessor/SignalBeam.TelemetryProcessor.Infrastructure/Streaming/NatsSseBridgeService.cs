using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Streaming;

/// <summary>
/// Background service that subscribes to NATS telemetry metrics and fans out
/// to active SSE connections via the SseConnectionManager.
/// Uses Core NATS (not JetStream) since live streaming doesn't need persistence.
/// </summary>
public class NatsSseBridgeService : BackgroundService
{
    private readonly ILogger<NatsSseBridgeService> _logger;
    private readonly NatsConnection _connection;
    private readonly SseConnectionManager _connectionManager;

    private const string MetricsSubject = "signalbeam.telemetry.metrics.>";

    public NatsSseBridgeService(
        ILogger<NatsSseBridgeService> logger,
        NatsConnection connection,
        SseConnectionManager connectionManager)
    {
        _logger = logger;
        _connection = connection;
        _connectionManager = connectionManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NATS SSE Bridge starting, subscribing to {Subject}", MetricsSubject);

        try
        {
            await foreach (var msg in _connection.SubscribeAsync<byte[]>(MetricsSubject, cancellationToken: stoppingToken))
            {
                try
                {
                    if (msg.Data is null)
                        continue;

                    var message = JsonSerializer.Deserialize<DeviceMetricsMessage>(msg.Data);
                    if (message is null)
                        continue;

                    var deviceId = message.DeviceId.ToString();
                    _connectionManager.Publish(deviceId, message);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize metrics message for SSE bridge");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NATS SSE Bridge stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in NATS SSE Bridge");
            throw;
        }
    }
}
