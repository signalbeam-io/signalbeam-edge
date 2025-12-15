using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.Commands;

public class UpdateDeviceHandlerTests
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly UpdateDeviceHandler _handler;

    public UpdateDeviceHandlerTests()
    {
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _handler = new UpdateDeviceHandler(_deviceRepository);
    }

    [Fact]
    public async Task Handle_ShouldUpdateDeviceName_WhenNameProvided()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = Device.Register(
            new DeviceId(deviceId),
            new TenantId(Guid.NewGuid()),
            "Old Name",
            DateTimeOffset.UtcNow);

        var command = new UpdateDeviceCommand(
            DeviceId: deviceId,
            Name: "New Name",
            Metadata: null);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("New Name");
        device.Name.Should().Be("New Name");

        await _deviceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateMetadata_WhenMetadataProvided()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = Device.Register(
            new DeviceId(deviceId),
            new TenantId(Guid.NewGuid()),
            "Device",
            DateTimeOffset.UtcNow,
            "{\"old\":\"data\"}");

        var newMetadata = "{\"new\":\"data\",\"location\":\"lab\"}";
        var command = new UpdateDeviceCommand(
            DeviceId: deviceId,
            Name: null,
            Metadata: newMetadata);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Metadata.Should().Be(newMetadata);
        device.Metadata.Should().Be(newMetadata);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeviceNotFound()
    {
        // Arrange
        var command = new UpdateDeviceCommand(
            DeviceId: Guid.NewGuid(),
            Name: "New Name",
            Metadata: null);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");

        await _deviceRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
