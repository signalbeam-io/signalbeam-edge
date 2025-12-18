using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.MessageHandlers;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;
using SignalBeam.TelemetryProcessor.Tests.Integration.Infrastructure;

namespace SignalBeam.TelemetryProcessor.Tests.Integration;

/// <summary>
/// Integration tests for NATS message consumption and processing.
/// Note: These tests require a running NATS server with JetStream enabled at nats://localhost:4222
/// </summary>
[Collection("NATS Integration Tests")]
public class NatsConsumerIntegrationTests : IClassFixture<TelemetryProcessorTestFixture>, IAsyncLifetime
{
    private readonly TelemetryProcessorTestFixture _fixture;
    private NatsConnection? _natsConnection;
    private INatsJSContext? _jetStreamContext;
    private readonly string _testStreamName = "TEST_TELEMETRY";
    private readonly string _testSubject = "test.telemetry.>";

    public NatsConsumerIntegrationTests(TelemetryProcessorTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Skip if NATS is not available
        if (!_fixture.IsNatsAvailable())
        {
            return;
        }

        var opts = new NatsOpts
        {
            Url = _fixture.NatsUrl,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

        _natsConnection = new NatsConnection(opts);
        await _natsConnection.ConnectAsync();
        _jetStreamContext = new NatsJSContext(_natsConnection);

        // Create a test stream
        try
        {
            await _jetStreamContext.DeleteStreamAsync(_testStreamName);
        }
        catch
        {
            // Stream might not exist
        }

        var config = new StreamConfig(_testStreamName, new[] { _testSubject })
        {
            Retention = StreamConfigRetention.Limits,
            MaxAge = TimeSpan.FromMinutes(5),
            Storage = StreamConfigStorage.Memory
        };

        await _jetStreamContext.CreateStreamAsync(config);
    }

    public async Task DisposeAsync()
    {
        if (_jetStreamContext != null)
        {
            try
            {
                await _jetStreamContext.DeleteStreamAsync(_testStreamName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        if (_natsConnection != null)
        {
            await _natsConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task PublishDeviceMetricsMessage_IsConsumedAndProcessed()
    {
        // Skip if NATS is not available
        if (!_fixture.IsNatsAvailable() || _jetStreamContext == null || _natsConnection == null)
        {
            // Skip test - NATS not available in this environment
            return;
        }

        // Arrange
        var deviceId = Guid.NewGuid();
        var message = new DeviceMetricsMessage(
            deviceId,
            DateTimeOffset.UtcNow,
            45.5,  // CpuUsage
            60.2,  // MemoryUsage
            75.8,  // DiskUsage
            3600,  // UptimeSeconds
            3      // RunningContainers
        );

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);

        // Act - Publish message to test stream
        await _jetStreamContext.PublishAsync($"test.telemetry.metrics.{deviceId}", messageBytes);

        // Give some time for message to be processed
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert - Check database for processed metrics
        using var dbContext = _fixture.CreateDbContext();
        var savedMetrics = dbContext.DeviceMetrics
            .Where(m => m.DeviceId == new DeviceId(deviceId))
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefault();

        // Note: This assertion depends on the message handler actually processing the message
        // If the consumer is not running in this test, this will be null
        // For a real integration test, you'd want to start the Host service
        savedMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishDeviceHeartbeatMessage_IsConsumedAndProcessed()
    {
        // Skip if NATS is not available
        if (!_fixture.IsNatsAvailable() || _jetStreamContext == null || _natsConnection == null)
        {
            return;
        }

        // Arrange
        var deviceId = Guid.NewGuid();
        var message = new DeviceHeartbeatMessage(
            deviceId,
            DateTimeOffset.UtcNow,
            "Online"
        );

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);

        // Act - Publish message to test stream
        await _jetStreamContext.PublishAsync($"test.telemetry.heartbeat.{deviceId}", messageBytes);

        // Give some time for message to be processed
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert - Check database for processed heartbeat
        using var dbContext = _fixture.CreateDbContext();
        var savedHeartbeat = dbContext.DeviceHeartbeats
            .Where(h => h.DeviceId == new DeviceId(deviceId))
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefault();

        // Note: This depends on the consumer service running
        savedHeartbeat.Should().NotBeNull();
    }

    [Fact]
    public void NatsConnection_CanConnectToServer()
    {
        // Skip if NATS is not available
        if (!_fixture.IsNatsAvailable())
        {
            return;
        }

        // Assert
        _natsConnection.Should().NotBeNull();
        _natsConnection!.ConnectionState.Should().Be(NatsConnectionState.Open);
    }

    [Fact]
    public async Task JetStream_CanCreateConsumer()
    {
        // Skip if NATS is not available
        if (!_fixture.IsNatsAvailable() || _jetStreamContext == null)
        {
            return;
        }

        // Arrange & Act
        var consumer = await _jetStreamContext.CreateOrUpdateConsumerAsync(
            _testStreamName,
            new ConsumerConfig
            {
                Name = "test-consumer",
                DurableName = "test-consumer",
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                FilterSubject = _testSubject
            });

        // Assert
        consumer.Should().NotBeNull();
        consumer.Info.Config.Name.Should().Be("test-consumer");
    }
}
