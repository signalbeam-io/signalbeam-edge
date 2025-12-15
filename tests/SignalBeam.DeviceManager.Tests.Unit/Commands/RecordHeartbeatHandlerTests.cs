using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.Commands;

public class RecordHeartbeatHandlerTests
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly RecordHeartbeatHandler _handler;

    public RecordHeartbeatHandlerTests()
    {
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _handler = new RecordHeartbeatHandler(_deviceRepository);
    }

    [Fact]
    public async Task Handle_ShouldRecordHeartbeat_WhenDeviceExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = Device.Register(
            new DeviceId(deviceId),
            new TenantId(Guid.NewGuid()),
            "Test Device",
            DateTimeOffset.UtcNow.AddHours(-1));

        var timestamp = DateTimeOffset.UtcNow;
        var command = new RecordHeartbeatCommand(
            DeviceId: deviceId,
            Timestamp: timestamp);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.LastSeenAt.Should().Be(timestamp);
        device.LastSeenAt.Should().Be(timestamp);
        device.Status.Should().Be(DeviceStatus.Online);

        await _deviceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTransitionToOnline_WhenDeviceWasOffline()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = Device.Register(
            new DeviceId(deviceId),
            new TenantId(Guid.NewGuid()),
            "Test Device",
            DateTimeOffset.UtcNow.AddHours(-2));

        // Mark device as offline
        device.MarkAsOffline(DateTimeOffset.UtcNow.AddHours(-1));

        var command = new RecordHeartbeatCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        device.Status.Should().Be(DeviceStatus.Online);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeviceNotFound()
    {
        // Arrange
        var command = new RecordHeartbeatCommand(
            DeviceId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
    }
}
