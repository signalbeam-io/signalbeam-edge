using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Tests.Unit.Commands;

public class RegisterDeviceHandlerTests
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceRegistrationTokenRepository _tokenRepository;
    private readonly IRegistrationTokenService _tokenService;
    private readonly IDeviceQuotaValidator _quotaValidator;
    private readonly RegisterDeviceHandler _handler;

    public RegisterDeviceHandlerTests()
    {
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _tokenRepository = Substitute.For<IDeviceRegistrationTokenRepository>();
        _tokenService = Substitute.For<IRegistrationTokenService>();
        _quotaValidator = Substitute.For<IDeviceQuotaValidator>();

        // By default, quota check passes
        _quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _handler = new RegisterDeviceHandler(_deviceRepository, _tokenRepository, _tokenService, _quotaValidator);
    }

    [Fact]
    public async Task Handle_ShouldRegisterDevice_WhenDeviceDoesNotExist()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: "Test Device",
            Metadata: "{\"location\":\"lab\"}");

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DeviceId.Should().Be(command.DeviceId!.Value);
        result.Value.Name.Should().Be(command.Name);

        await _deviceRepository.Received(1).AddAsync(
            Arg.Is<Device>(d => d.Id.Value == command.DeviceId),
            Arg.Any<CancellationToken>());

        await _deviceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeviceAlreadyExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: deviceId,
            Name: "Test Device");

        var existingDevice = Device.Register(
            new DeviceId(deviceId),
            new TenantId(Guid.NewGuid()),
            "Existing Device",
            DateTimeOffset.UtcNow);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(existingDevice);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_ALREADY_EXISTS");
        result.Error.Type.Should().Be(SignalBeam.Shared.Infrastructure.Results.ErrorType.Conflict);

        await _deviceRepository.DidNotReceive().AddAsync(
            Arg.Any<Device>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAcceptMetadata_WhenProvided()
    {
        // Arrange
        var metadata = "{\"serialNumber\":\"12345\",\"location\":\"warehouse-A\"}";
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: "Test Device",
            Metadata: metadata);

        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Verify device was added with metadata
        await _deviceRepository.Received(1).AddAsync(
            Arg.Is<Device>(d => d.Metadata == metadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeviceQuotaExceeded()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new RegisterDeviceCommand(
            TenantId: tenantId,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Mock quota validator to return failure
        var quotaError = Error.Validation(
            "DEVICE_QUOTA_EXCEEDED",
            "Device quota exceeded. Your Free plan allows up to 5 devices. Please upgrade your subscription.");

        _quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(quotaError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_QUOTA_EXCEEDED");
        result.Error.Type.Should().Be(SignalBeam.Shared.Infrastructure.Results.ErrorType.Validation);
        result.Error.Message.Should().Contain("quota exceeded");
        result.Error.Message.Should().Contain("upgrade");

        // Verify device was NOT added when quota exceeded
        await _deviceRepository.DidNotReceive().AddAsync(
            Arg.Any<Device>(),
            Arg.Any<CancellationToken>());

        await _deviceRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        // Verify quota check was called with correct tenant ID
        await _quotaValidator.Received(1).CheckDeviceQuotaAsync(
            Arg.Is<TenantId>(t => t.Value == tenantId),
            Arg.Any<CancellationToken>());
    }
}
