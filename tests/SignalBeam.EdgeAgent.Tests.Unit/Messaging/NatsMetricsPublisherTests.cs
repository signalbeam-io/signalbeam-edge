using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Messaging;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Messaging;

public class NatsMetricsPublisherTests
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly NatsMetricsPublisher _sut;

    public NatsMetricsPublisherTests()
    {
        _messagePublisher = Substitute.For<IMessagePublisher>();
        var logger = Substitute.For<ILogger<NatsMetricsPublisher>>();
        _sut = new NatsMetricsPublisher(_messagePublisher, logger);
    }

    [Fact]
    public async Task PublishMetricsAsync_PublishesToCorrectSubject()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(45.5, 60.0, 75.0, 3600);

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 3);

        // Assert
        await _messagePublisher.Received(1).PublishAsync(
            $"signalbeam.telemetry.metrics.{deviceId}",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishMetricsAsync_MessageContainsCorrectFields()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(45.5, 60.0, 75.0, 3600);
        object? capturedMessage = null;

        await _messagePublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Do<object>(msg => capturedMessage = msg),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 3);

        // Assert
        capturedMessage.Should().NotBeNull();

        var json = System.Text.Json.JsonSerializer.Serialize(capturedMessage,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        json.Should().Contain($"\"deviceId\":\"{deviceId}\"");
        json.Should().Contain("\"cpuUsage\":45.5");
        json.Should().Contain("\"memoryUsage\":60");
        json.Should().Contain("\"diskUsage\":75");
        json.Should().Contain("\"uptimeSeconds\":3600");
        json.Should().Contain("\"runningContainers\":3");
    }

    [Fact]
    public async Task PublishMetricsAsync_IncludesTimestamp()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(10.0, 20.0, 30.0, 100);
        object? capturedMessage = null;

        await _messagePublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Do<object>(msg => capturedMessage = msg),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 0);

        // Assert
        var json = System.Text.Json.JsonSerializer.Serialize(capturedMessage,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        json.Should().Contain("\"timestamp\":");
    }
}
