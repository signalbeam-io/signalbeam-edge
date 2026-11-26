using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.Queries;

public class GetDeviceTenantHandlerTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();

    [Fact]
    public async Task Handle_KnownDevice_ReturnsItsTenant()
    {
        // Arrange
        var deviceId = new DeviceId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        _devices.GetByIdAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns(Device.Register(deviceId, tenantId, "sim-lab-rpi-001", DateTimeOffset.UtcNow));

        // Act
        var result = await new GetDeviceTenantHandler(_devices).Handle(new GetDeviceTenantQuery(deviceId.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TenantId.Should().Be(tenantId.Value);
    }

    [Fact]
    public async Task Handle_UnknownDevice_ReturnsNotFound()
    {
        // Act
        var result = await new GetDeviceTenantHandler(_devices).Handle(new GetDeviceTenantQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
    }
}
