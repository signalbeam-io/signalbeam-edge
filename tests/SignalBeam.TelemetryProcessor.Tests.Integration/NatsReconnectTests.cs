using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging.Options;
using SignalBeam.TelemetryProcessor.Infrastructure.Streaming;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Verifies #387: the NATS background services survive a broker outage at startup
/// and recover on their own once NATS becomes reachable — no redeploy needed.
/// Each test picks a free host port so NATS can be started *after* the service.
/// </summary>
public class NatsReconnectTests
{
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task NatsConsumerService_WhenNatsIsDownAtStartup_KeepsRetryingWithoutCrashing()
    {
        var port = GetFreePort();
        await using var connection = CreateConnection(port);
        var service = CreateConsumerService(connection);

        await service.StartAsync(CancellationToken.None);

        // Long enough to cover the initial failure and at least one retry.
        await Task.Delay(TimeSpan.FromSeconds(8));

        service.ExecuteTask.Should().NotBeNull();
        service.ExecuteTask!.IsCompleted.Should().BeFalse(
            "the service should keep retrying while NATS is down instead of dying");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NatsConsumerService_WhenNatsComesUpAfterOutage_CreatesStreamsAndConsumers()
    {
        var port = GetFreePort();
        await using var connection = CreateConnection(port);
        var service = CreateConsumerService(connection);

        // Start with NATS down so the service enters its retry loop.
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await using var nats = BuildNatsContainer(port);
        await nats.StartAsync();

        // The service should now create the streams and durable consumers.
        await using var verifyConnection = CreateConnection(port);
        var verifyJs = new NatsJSContext(verifyConnection);

        using var cts = new CancellationTokenSource(RecoveryTimeout);
        var consumerNames = new[]
        {
            ("DEVICE_METRICS", "telemetry-processor-metrics"),
            ("DEVICE_HEARTBEATS", "telemetry-processor-heartbeats")
        };

        foreach (var (stream, consumerName) in consumerNames)
        {
            var found = false;
            while (!found)
            {
                cts.Token.IsCancellationRequested.Should().BeFalse(
                    $"consumer {consumerName} should be (re)created within {RecoveryTimeout.TotalSeconds}s of NATS coming up");
                try
                {
                    await verifyJs.GetConsumerAsync(stream, consumerName, cts.Token);
                    found = true;
                }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
                }
            }
        }

        service.ExecuteTask!.IsCompleted.Should().BeFalse("the consumers should be running, not completed");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NatsSseBridgeService_WhenNatsComesUpAfterOutage_ResumesFanOut()
    {
        var port = GetFreePort();
        await using var connection = CreateConnection(port);
        var connectionManager = new SseConnectionManager(NullLogger<SseConnectionManager>.Instance);
        var service = new NatsSseBridgeService(
            NullLogger<NatsSseBridgeService>.Instance, connection, connectionManager);

        // Start with NATS down so the bridge enters its resubscribe loop.
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await using var nats = BuildNatsContainer(port);
        await nats.StartAsync();

        var deviceId = Guid.NewGuid();
        var reader = connectionManager.Subscribe(deviceId.ToString());
        var message = new DeviceMetricsMessage(
            deviceId, DateTimeOffset.UtcNow, 50.0, 60.0, 70.0, 3600, 3);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);

        await using var publisher = CreateConnection(port);

        // Publish repeatedly — the bridge may still be waiting out its backoff.
        using var cts = new CancellationTokenSource(RecoveryTimeout);
        DeviceMetricsMessage? received = null;
        while (received is null && !cts.Token.IsCancellationRequested)
        {
            await publisher.PublishAsync(
                $"signalbeam.telemetry.metrics.{deviceId}", payload, cancellationToken: cts.Token);

            if (await WaitForMessageAsync(reader, TimeSpan.FromSeconds(2)) is { } msg)
            {
                received = msg;
            }
        }

        received.Should().NotBeNull(
            $"the SSE bridge should resubscribe and fan out messages within {RecoveryTimeout.TotalSeconds}s of NATS coming up");
        received!.DeviceId.Should().Be(deviceId);

        await service.StopAsync(CancellationToken.None);
    }

    private static async Task<DeviceMetricsMessage?> WaitForMessageAsync(
        System.Threading.Channels.ChannelReader<DeviceMetricsMessage> reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static NatsConsumerService CreateConsumerService(NatsConnection connection)
    {
        var scopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new NatsConsumerService(
            NullLogger<NatsConsumerService>.Instance,
            connection,
            new NatsJSContext(connection),
            Microsoft.Extensions.Options.Options.Create(new NatsOptions()),
            scopeFactory);
    }

    private static NatsConnection CreateConnection(int port) =>
        new(new NatsOpts
        {
            Url = $"nats://127.0.0.1:{port}",
            ConnectTimeout = TimeSpan.FromSeconds(2),
            MaxReconnectRetry = -1,
            ReconnectWaitMin = TimeSpan.FromSeconds(1),
            ReconnectWaitMax = TimeSpan.FromSeconds(5)
        });

    private static IContainer BuildNatsContainer(int hostPort) =>
        new ContainerBuilder("nats:2.10-alpine")
            .WithCommand("--jetstream")
            .WithPortBinding(hostPort, 4222)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server is ready"))
            .Build();

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
