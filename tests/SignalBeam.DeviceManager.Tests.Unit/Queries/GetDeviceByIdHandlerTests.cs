using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.Queries;

public class GetDeviceByIdHandlerTests
{
    private readonly IDeviceQueryRepository _queryRepository;
    private readonly GetDeviceByIdHandler _handler;

    public GetDeviceByIdHandlerTests()
    {
        _queryRepository = Substitute.For<IDeviceQueryRepository>();
        _handler = new GetDeviceByIdHandler(_queryRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnDevice_WhenDeviceExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var device = Device.Register(
            new DeviceId(deviceId),
            new TenantId(tenantId),
            "Test Device",
            DateTimeOffset.UtcNow,
            "{\"location\":\"lab\"}");

        device.AddTag("production");
        device.AddTag("rpi");

        var query = new GetDeviceByIdQuery(deviceId);

        _queryRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(deviceId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.Name.Should().Be("Test Device");
        result.Value.Metadata.Should().Be("{\"location\":\"lab\"}");
        result.Value.Tags.Should().Contain("production");
        result.Value.Tags.Should().Contain("rpi");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeviceNotFound()
    {
        // Arrange
        var query = new GetDeviceByIdQuery(Guid.NewGuid());

        _queryRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
        result.Error.Type.Should().Be(SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound);
    }
}
