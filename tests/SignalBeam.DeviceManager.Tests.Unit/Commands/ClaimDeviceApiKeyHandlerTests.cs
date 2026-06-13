using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Tests.Unit.Commands;

public class ClaimDeviceApiKeyHandlerTests
{
    private const string ValidToken = "sbt_abc12345_secret";
    private const string TokenPrefix = "sbt_abc12345";

    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly IDeviceApiKeyRepository _apiKeyRepository = Substitute.For<IDeviceApiKeyRepository>();
    private readonly IDeviceRegistrationTokenRepository _tokenRepository = Substitute.For<IDeviceRegistrationTokenRepository>();
    private readonly IDeviceApiKeyService _apiKeyService = Substitute.For<IDeviceApiKeyService>();
    private readonly IRegistrationTokenService _tokenService = Substitute.For<IRegistrationTokenService>();
    private readonly ClaimDeviceApiKeyHandler _handler;

    private readonly DeviceId _deviceId = DeviceId.New();
    private readonly TenantId _tenantId = TenantId.New();

    public ClaimDeviceApiKeyHandlerTests()
    {
        _handler = new ClaimDeviceApiKeyHandler(
            _deviceRepository, _apiKeyRepository, _tokenRepository, _apiKeyService, _tokenService);

        _apiKeyRepository.GetActiveByDeviceIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(new List<DeviceApiKey>());
        _apiKeyService.GenerateApiKey(Arg.Any<DeviceId>())
            .Returns(("sb_device_ab12_secret", "key-hash", "ab12"));
        _tokenService.ValidateToken(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    private Device ApprovedDevice()
    {
        var device = Device.Register(_deviceId, _tenantId, "pi-5", DateTimeOffset.UtcNow);
        device.ApproveRegistration(DateTimeOffset.UtcNow);
        return device;
    }

    private DeviceRegistrationToken TokenUsedBy(DeviceId deviceId, TenantId? tenantId = null)
    {
        var token = DeviceRegistrationToken.Create(
            tenantId ?? _tenantId, "token-hash", TokenPrefix, DateTimeOffset.UtcNow.AddDays(1));
        token.MarkAsUsed(deviceId);
        return token;
    }

    [Fact]
    public async Task Handle_ApprovedDeviceWithBoundToken_ReturnsKeyAndMarksClaimed()
    {
        var device = ApprovedDevice();
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);
        _tokenRepository.GetByPrefixAsync(TokenPrefix, Arg.Any<CancellationToken>()).Returns(TokenUsedBy(_deviceId));

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().Be("sb_device_ab12_secret");
        device.KeyClaimedAt.Should().NotBeNull();
        device.IsKeyClaimAvailable.Should().BeFalse();
        await _apiKeyRepository.Received(1).AddAsync(Arg.Any<DeviceApiKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeviceNotApproved_ReturnsForbidden()
    {
        var device = Device.Register(_deviceId, _tenantId, "pi-5", DateTimeOffset.UtcNow); // still Pending
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("DEVICE_NOT_APPROVED");
    }

    [Fact]
    public async Task Handle_KeyAlreadyClaimed_ReturnsConflict()
    {
        var device = ApprovedDevice();
        device.MarkKeyClaimed(DateTimeOffset.UtcNow);
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("KEY_ALREADY_CLAIMED");
    }

    [Fact]
    public async Task Handle_TokenUsedByDifferentDevice_ReturnsUnauthorized()
    {
        var device = ApprovedDevice();
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);
        // Token was used by some OTHER device
        _tokenRepository.GetByPrefixAsync(TokenPrefix, Arg.Any<CancellationToken>()).Returns(TokenUsedBy(DeviceId.New()));

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("RegistrationToken.DeviceMismatch");
        device.KeyClaimedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InvalidTokenSecret_ReturnsUnauthorized()
    {
        var device = ApprovedDevice();
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);
        _tokenRepository.GetByPrefixAsync(TokenPrefix, Arg.Any<CancellationToken>()).Returns(TokenUsedBy(_deviceId));
        _tokenService.ValidateToken(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_DeviceNotFound_ReturnsNotFound()
    {
        _deviceRepository.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns((Device?)null);

        var result = await _handler.Handle(new ClaimDeviceApiKeyCommand(_deviceId.Value, ValidToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
