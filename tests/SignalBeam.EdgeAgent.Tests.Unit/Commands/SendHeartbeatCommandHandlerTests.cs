using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Tests.Unit.Commands;

public class SendHeartbeatCommandHandlerTests
{
    private readonly ICloudClient _cloudClient;
    private readonly IMetricsCollector _metricsCollector;
    private readonly SendHeartbeatCommandHandler _handler;

    public SendHeartbeatCommandHandlerTests()
    {
        _cloudClient = Substitute.For<ICloudClient>();
        _metricsCollector = Substitute.For<IMetricsCollector>();
        _handler = new SendHeartbeatCommandHandler(_cloudClient, _metricsCollector);
    }

    [Fact]
    public async Task Handle_ValidCommand_CollectsMetricsAndSendsHeartbeat()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var command = new SendHeartbeatCommand(deviceId);

        var metrics = new DeviceMetrics(
            CpuUsagePercent: 45.5,
            MemoryUsagePercent: 60.0,
            DiskUsagePercent: 75.0,
            UptimeSeconds: 3600);

        _metricsCollector.CollectMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(metrics);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _metricsCollector.Received(1).CollectMetricsAsync(Arg.Any<CancellationToken>());
        await _cloudClient.Received(1).SendHeartbeatAsync(
            Arg.Is<DeviceHeartbeat>(h =>
                h.DeviceId == deviceId &&
                h.Metrics == metrics),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MetricsCollectorThrows_ReturnsFailureResult()
    {
        // Arrange
        var command = new SendHeartbeatCommand(Guid.NewGuid());

        _metricsCollector.CollectMetricsAsync(Arg.Any<CancellationToken>())
            .Returns<DeviceMetrics>(_ => throw new Exception("Metrics collection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Heartbeat.Failed");
    }
}
