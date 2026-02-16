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
    private readonly ConcurrentDictionary<string, SubscriberSet> _connections = new();
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

        var set = _connections.GetOrAdd(deviceId, _ => new SubscriberSet());
        set.Add(channel);

        _logger.LogDebug("SSE client subscribed for device {DeviceId}. Active connections: {Count}", deviceId, set.Count);

        return channel.Reader;
    }

    /// <summary>
    /// Unsubscribe a channel from a device's metrics stream.
    /// </summary>
    public void Unsubscribe(string deviceId, ChannelReader<DeviceMetricsMessage> reader)
    {
        if (!_connections.TryGetValue(deviceId, out var set))
            return;

        set.Remove(reader);

        // Clean up empty entries to prevent memory growth
        if (set.Count == 0)
        {
            _connections.TryRemove(deviceId, out _);
        }

        _logger.LogDebug("SSE client unsubscribed for device {DeviceId}. Remaining: {Count}", deviceId, set.Count);
    }

    /// <summary>
    /// Publish a metrics message to all SSE clients subscribed to the given device.
    /// </summary>
    public void Publish(string deviceId, DeviceMetricsMessage message)
    {
        if (!_connections.TryGetValue(deviceId, out var set))
            return;

        set.Publish(message, _logger, deviceId);
    }

    /// <summary>
    /// Thread-safe set of subscriber channels with proper add/remove support.
    /// </summary>
    private sealed class SubscriberSet
    {
        private readonly Lock _lock = new();
        private readonly HashSet<Channel<DeviceMetricsMessage>> _channels = [];

        public int Count
        {
            get
            {
                lock (_lock) { return _channels.Count; }
            }
        }

        public void Add(Channel<DeviceMetricsMessage> channel)
        {
            lock (_lock) { _channels.Add(channel); }
        }

        public void Remove(ChannelReader<DeviceMetricsMessage> reader)
        {
            lock (_lock)
            {
                Channel<DeviceMetricsMessage>? target = null;
                foreach (var ch in _channels)
                {
                    if (ch.Reader == reader)
                    {
                        target = ch;
                        break;
                    }
                }

                if (target is not null)
                {
                    _channels.Remove(target);
                    target.Writer.TryComplete();
                }
            }
        }

        public void Publish(DeviceMetricsMessage message, ILogger logger, string deviceId)
        {
            lock (_lock)
            {
                foreach (var channel in _channels)
                {
                    if (!channel.Writer.TryWrite(message))
                    {
                        logger.LogDebug("Dropped metrics message for device {DeviceId} (channel full or completed)", deviceId);
                    }
                }
            }
        }
    }
}
