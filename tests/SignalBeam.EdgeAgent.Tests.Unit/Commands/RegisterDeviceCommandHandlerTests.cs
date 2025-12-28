using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Tests.Unit.Commands;

public class RegisterDeviceCommandHandlerTests
{
    private readonly ICloudClient _cloudClient;
    private readonly IDeviceCredentialsStore _credentialsStore;
    private readonly ILogger<RegisterDeviceCommandHandler> _logger;
    private readonly RegisterDeviceCommandHandler _handler;

    public RegisterDeviceCommandHandlerTests()
    {
        _cloudClient = Substitute.For<ICloudClient>();
        _credentialsStore = Substitute.For<IDeviceCredentialsStore>();
        _logger = Substitute.For<ILogger<RegisterDeviceCommandHandler>>();
        _handler = new RegisterDeviceCommandHandler(_cloudClient, _credentialsStore, _logger);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            Guid.NewGuid(),
            "device-123",
            "token-abc",
            "my-device",
            "linux");

        var expectedResponse = new DeviceRegistrationResponse(
            Guid.NewGuid(),
            "test-device",
            "Pending",
            DateTimeOffset.UtcNow);

        _cloudClient.RegisterDeviceAsync(
                Arg.Any<DeviceRegistrationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResponse);
        await _cloudClient.Received(1).RegisterDeviceAsync(
            Arg.Is<DeviceRegistrationRequest>(r =>
                r.TenantId == command.TenantId &&
                r.DeviceId == command.DeviceId &&
                r.RegistrationToken == command.RegistrationToken),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CloudClientThrows_ReturnsFailureResult()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            Guid.NewGuid(),
            "device-123",
            "token-abc");

        _cloudClient.RegisterDeviceAsync(
                Arg.Any<DeviceRegistrationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns<DeviceRegistrationResponse>(_ => throw new Exception("Network error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("DeviceRegistration.Failed");
    }
}
