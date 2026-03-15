using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Messaging;

public class NatsMetricsPublisherTests
{
    private readonly INatsJSContext _jetStream;
    private readonly NatsMetricsPublisher _sut;
    private string? _capturedSubject;
    private string? _capturedData;

    public NatsMetricsPublisherTests()
    {
        _jetStream = Substitute.For<INatsJSContext>();

        // Configure JetStream mock to return successful ack and capture arguments
        _jetStream.PublishAsync(
            Arg.Do<string>(s => _capturedSubject = s),
            Arg.Do<string>(d => _capturedData = d),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new PubAckResponse());

        var logger = Substitute.For<ILogger<NatsMetricsPublisher>>();
        _sut = new NatsMetricsPublisher(_jetStream, logger);
    }

    [Fact]
    public async Task PublishMetricsAsync_PublishesToCorrectJetStreamSubject()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(45.5, 60.0, 75.0, 3600);

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 3);

        // Assert
        _capturedSubject.Should().Be($"signalbeam.telemetry.metrics.{deviceId}");
    }

    [Fact]
    public async Task PublishMetricsAsync_SerializesMessageAsCamelCaseJson()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(45.5, 60.0, 75.0, 3600);

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 3);

        // Assert
        _capturedData.Should().NotBeNull();
        _capturedData.Should().Contain($"\"deviceId\":\"{deviceId}\"");
        _capturedData.Should().Contain("\"cpuUsage\":45.5");
        _capturedData.Should().Contain("\"memoryUsage\":60");
        _capturedData.Should().Contain("\"diskUsage\":75");
        _capturedData.Should().Contain("\"uptimeSeconds\":3600");
        _capturedData.Should().Contain("\"runningContainers\":3");
    }

    [Fact]
    public async Task PublishMetricsAsync_IncludesTimestamp()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var metrics = new DeviceMetrics(10.0, 20.0, 30.0, 100);

        // Act
        await _sut.PublishMetricsAsync(deviceId, metrics, runningContainers: 0);

        // Assert
        _capturedData.Should().Contain("\"timestamp\":");
    }
}
