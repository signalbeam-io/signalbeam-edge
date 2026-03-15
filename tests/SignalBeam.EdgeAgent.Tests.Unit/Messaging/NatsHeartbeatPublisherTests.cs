using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Infrastructure.Messaging;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Messaging;

public class NatsHeartbeatPublisherTests
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly NatsHeartbeatPublisher _sut;

    public NatsHeartbeatPublisherTests()
    {
        _messagePublisher = Substitute.For<IMessagePublisher>();
        var logger = Substitute.For<ILogger<NatsHeartbeatPublisher>>();
        _sut = new NatsHeartbeatPublisher(_messagePublisher, logger);
    }

    [Fact]
    public async Task PublishHeartbeatAsync_PublishesToCorrectSubject()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        await _sut.PublishHeartbeatAsync(deviceId, "online", "192.168.1.100");

        // Assert
        await _messagePublisher.Received(1).PublishAsync(
            $"signalbeam.devices.heartbeat.{deviceId}",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishHeartbeatAsync_MessageContainsCorrectFields()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        object? capturedMessage = null;

        await _messagePublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Do<object>(msg => capturedMessage = msg),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.PublishHeartbeatAsync(deviceId, "online", "192.168.1.100");

        // Assert
        capturedMessage.Should().NotBeNull();

        var json = System.Text.Json.JsonSerializer.Serialize(capturedMessage,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        json.Should().Contain($"\"deviceId\":\"{deviceId}\"");
        json.Should().Contain("\"status\":\"online\"");
        json.Should().Contain("\"ipAddress\":\"192.168.1.100\"");
        json.Should().Contain("\"timestamp\":");
    }

    [Fact]
    public async Task PublishHeartbeatAsync_WithNullIpAddress_PublishesSuccessfully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        object? capturedMessage = null;

        await _messagePublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Do<object>(msg => capturedMessage = msg),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.PublishHeartbeatAsync(deviceId, "online");

        // Assert
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());

        var json = System.Text.Json.JsonSerializer.Serialize(capturedMessage,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        json.Should().Contain("\"ipAddress\":null");
    }
}
