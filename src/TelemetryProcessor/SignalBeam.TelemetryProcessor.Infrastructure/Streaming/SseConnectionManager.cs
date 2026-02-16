using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Streaming;

/// <summary>
/// Thread-safe manager that tracks active SSE connections per device and fans out metrics messages.
/// </summary>
public sealed class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<DeviceMetricsMessage>>> _connections = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to metrics for a specific device. Returns a ChannelReader to consume messages from.
    /// </summary>
    public ChannelReader<DeviceMetricsMessage> Subscribe(string deviceId)
    {
        var channel = Channel.CreateBounded<DeviceMetricsMessage>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var bag = _connections.GetOrAdd(deviceId, _ => new ConcurrentBag<Channel<DeviceMetricsMessage>>());
        bag.Add(channel);

        _logger.LogDebug("SSE client subscribed for device {DeviceId}. Active connections: {Count}", deviceId, bag.Count);

        return channel.Reader;
    }

    /// <summary>
    /// Unsubscribe a channel from a device's metrics stream.
    /// </summary>
    public void Unsubscribe(string deviceId, ChannelReader<DeviceMetricsMessage> reader)
    {
        if (!_connections.TryGetValue(deviceId, out var bag))
            return;

        // ConcurrentBag doesn't support removal, so rebuild without the target channel
        var remaining = new ConcurrentBag<Channel<DeviceMetricsMessage>>();
        foreach (var ch in bag)
        {
            if (ch.Reader != reader)
                remaining.Add(ch);
            else
                ch.Writer.TryComplete();
        }

        _connections.TryUpdate(deviceId, remaining, bag);

        _logger.LogDebug("SSE client unsubscribed for device {DeviceId}. Remaining: {Count}", deviceId, remaining.Count);
    }

    /// <summary>
    /// Publish a metrics message to all SSE clients subscribed to the given device.
    /// </summary>
    public void Publish(string deviceId, DeviceMetricsMessage message)
    {
        if (!_connections.TryGetValue(deviceId, out var bag))
            return;

        foreach (var channel in bag)
        {
            if (!channel.Writer.TryWrite(message))
            {
                _logger.LogDebug("Dropped metrics message for device {DeviceId} (channel full or completed)", deviceId);
            }
        }
    }
}
