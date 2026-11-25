using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Client.JetStream;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging;
using SignalBeam.TelemetryProcessor.Infrastructure.Messaging.Options;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Verifies #475: a JetStream message's <c>process</c> span continues the publisher's trace
/// (W3C traceparent injected by NATS.Net) and records processing failures.
/// </summary>
/// <remarks>
/// The consumer runs with an empty service provider, so resolving the message handler fails.
/// That exercises the error path without needing the database, and the trace assertions don't
/// depend on the handler succeeding.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class NatsConsumerTracingTests : IAsyncLifetime, IDisposable
{
    private const string NatsSourceName = "NATS.Net";
    private const string PublisherSourceName = "SignalBeam.Tests.NatsConsumerTracing";
    private const string HeartbeatProcessSpan = "process signalbeam.devices.heartbeat.>";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly ActivitySource _publisherSource = new(PublisherSourceName);
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;
    private readonly IContainer _nats = new ContainerBuilder("nats:2.10-alpine")
        .WithCommand("--jetstream")
        .WithPortBinding(4222, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server is ready"))
        .Build();

    private NatsConnection _consumerConnection = null!;
    private NatsConnection _publisherConnection = null!;
    private NatsConsumerService _service = null!;

    public NatsConsumerTracingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is NatsSourceName or PublisherSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public async Task InitializeAsync()
    {
        await _nats.StartAsync();
        var url = $"nats://{_nats.Hostname}:{_nats.GetMappedPublicPort(4222)}";
        _consumerConnection = new NatsConnection(new NatsOpts { Url = url });
        _publisherConnection = new NatsConnection(new NatsOpts { Url = url });

        var emptyScopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _service = new NatsConsumerService(
            NullLogger<NatsConsumerService>.Instance,
            _consumerConnection,
            new NatsJSContext(_consumerConnection),
            Microsoft.Extensions.Options.Options.Create(new NatsOptions()),
            emptyScopeFactory);

        await _service.StartAsync(CancellationToken.None);
        await WaitForConsumerAsync("DEVICE_HEARTBEATS", "telemetry-processor-heartbeats");
    }

    [Fact]
    public async Task ProcessSpan_ContinuesPublisherTrace_AndRecordsHandlerFailure()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new DeviceHeartbeatMessage(deviceId, DateTimeOffset.UtcNow, "Online"));

        // Act: publish inside a parent span, as an upstream service would.
        ActivityTraceId traceId;
        using (var parent = _publisherSource.StartActivity("upstream request")!)
        {
            traceId = parent.TraceId;
            await new NatsJSContext(_publisherConnection)
                .PublishAsync($"signalbeam.devices.heartbeat.{deviceId}", payload);
        }

        var process = await WaitForSpanAsync(HeartbeatProcessSpan, traceId);

        // Assert
        process.TraceId.Should().Be(traceId, "the consumer must continue the publisher's trace");
        process.ParentSpanId.Should().NotBe(default(ActivitySpanId));
        process.DisplayName.Should().NotContain(deviceId.ToString(), "span names must stay low-cardinality");
        process.GetTagItem("messaging.operation.type").Should().Be("process");
        process.GetTagItem("messaging.consumer.group.name").Should().Be("telemetry-processor-heartbeats");
        process.Status.Should().Be(ActivityStatusCode.Error);
        process.Events.Should().Contain(e => e.Name == "exception");
    }

    [Fact]
    public async Task ProcessSpan_InvalidJson_IsMarkedAsDeserializationFailure()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        ActivityTraceId traceId;
        using (var parent = _publisherSource.StartActivity("upstream request")!)
        {
            traceId = parent.TraceId;
            await new NatsJSContext(_publisherConnection)
                .PublishAsync($"signalbeam.devices.heartbeat.{deviceId}", Encoding.UTF8.GetBytes("{not json"));
        }

        var process = await WaitForSpanAsync(HeartbeatProcessSpan, traceId);

        // Assert
        process.Status.Should().Be(ActivityStatusCode.Error);
        process.StatusDescription.Should().Be("deserialization failed");
        process.Events.Should().Contain(e => e.Name == "exception");
    }

    private async Task<Activity> WaitForSpanAsync(string name, ActivityTraceId traceId)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!cts.IsCancellationRequested)
        {
            var match = _stopped.FirstOrDefault(a => a.DisplayName == name && a.TraceId == traceId);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        }

        throw new TimeoutException(
            $"No '{name}' span in trace {traceId} within {Timeout.TotalSeconds}s. Seen: "
            + string.Join(", ", _stopped.Select(a => $"{a.DisplayName} [{a.TraceId}]").Distinct()));
    }

    private async Task WaitForConsumerAsync(string stream, string consumer)
    {
        var js = new NatsJSContext(_publisherConnection);
        using var cts = new CancellationTokenSource(Timeout);
        while (true)
        {
            try
            {
                await js.GetConsumerAsync(stream, consumer, cts.Token);
                return;
            }
            catch (Exception) when (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
            }
        }
    }

    public async Task DisposeAsync()
    {
        await _service.StopAsync(CancellationToken.None);
        _service.Dispose();
        await _consumerConnection.DisposeAsync();
        await _publisherConnection.DisposeAsync();
        await _nats.DisposeAsync();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _publisherSource.Dispose();
    }
}
