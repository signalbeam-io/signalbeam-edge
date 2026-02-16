using System.Text.Json;
using SignalBeam.TelemetryProcessor.Infrastructure.Streaming;

namespace SignalBeam.TelemetryProcessor.Host.Endpoints;

/// <summary>
/// SSE streaming endpoints for real-time device metrics.
/// </summary>
public static class MetricsStreamEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapMetricsStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/devices/{deviceId}/metrics/stream", StreamDeviceMetrics)
            .WithTags("Metrics")
            .WithName("StreamDeviceMetrics")
            .WithSummary("Stream real-time device metrics via SSE");

        return app;
    }

    private static async Task StreamDeviceMetrics(
        HttpContext context,
        string deviceId,
        SseConnectionManager connectionManager,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var reader = connectionManager.Subscribe(deviceId);
        var eventId = 0L;
        var lastWriteTime = DateTimeOffset.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to read a message with a timeout for keepalive
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                try
                {
                    if (await reader.WaitToReadAsync(timeoutCts.Token))
                    {
                        while (reader.TryRead(out var message))
                        {
                            eventId++;
                            var json = JsonSerializer.Serialize(message, JsonOptions);
                            await context.Response.WriteAsync($"id: {eventId}\ndata: {json}\n\n", cancellationToken);
                            await context.Response.Body.FlushAsync(cancellationToken);
                            lastWriteTime = DateTimeOffset.UtcNow;
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout — send keepalive if no data was written recently
                    if (DateTimeOffset.UtcNow - lastWriteTime >= TimeSpan.FromSeconds(25))
                    {
                        await context.Response.WriteAsync(":keepalive\n\n", cancellationToken);
                        await context.Response.Body.FlushAsync(cancellationToken);
                        lastWriteTime = DateTimeOffset.UtcNow;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected
        }
        finally
        {
            connectionManager.Unsubscribe(deviceId, reader);
        }
    }
}
