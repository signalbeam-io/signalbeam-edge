using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Cloud;

namespace SignalBeam.EdgeAgent.Tests.Unit.Cloud;

public class KeyRotationServiceTests
{
    private const int ThresholdDays = 7;

    private readonly ICloudClient _cloudClient = Substitute.For<ICloudClient>();
    private readonly IDeviceCredentialsStore _store = Substitute.For<IDeviceCredentialsStore>();
    private readonly KeyRotationService _sut;

    private readonly Guid _deviceId = Guid.NewGuid();

    public KeyRotationServiceTests()
    {
        var logger = Substitute.For<ILogger<KeyRotationService>>();
        _sut = new KeyRotationService(_cloudClient, _store, logger, ThresholdDays);
    }

    private DeviceCredentials Credentials(string? apiKey, DateTimeOffset? expiresAt) => new()
    {
        DeviceId = _deviceId,
        ApiKey = apiKey,
        ApiKeyExpiresAt = expiresAt
    };

    [Fact]
    public async Task CheckAndRotate_KeyExpiringWithinThreshold_Rotates()
    {
        var credentials = Credentials("sb_device_old", DateTimeOffset.UtcNow.AddDays(3));
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(credentials);
        var newExpiry = DateTimeOffset.UtcNow.AddDays(90);
        _cloudClient.RotateApiKeyAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new ClaimedApiKey("sb_device_new", newExpiry));

        var rotated = await _sut.CheckAndRotateAsync();

        rotated.Should().BeTrue();
        credentials.ApiKey.Should().Be("sb_device_new");
        await _store.Received(1).SaveCredentialsAsync(
            Arg.Is<DeviceCredentials>(c => c.ApiKey == "sb_device_new"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRotate_KeyNotNearExpiry_DoesNotRotate()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Credentials("sb_device_old", DateTimeOffset.UtcNow.AddDays(60)));

        var rotated = await _sut.CheckAndRotateAsync();

        rotated.Should().BeFalse();
        await _cloudClient.DidNotReceive().RotateApiKeyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRotate_KeyAlreadyExpired_DoesNotRotate()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Credentials("sb_device_old", DateTimeOffset.UtcNow.AddDays(-1)));

        var rotated = await _sut.CheckAndRotateAsync();

        // Can't authenticate a rotation with an expired key.
        rotated.Should().BeFalse();
        await _cloudClient.DidNotReceive().RotateApiKeyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRotate_NoExpiry_DoesNotRotate()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Credentials("sb_device_old", expiresAt: null));

        var rotated = await _sut.CheckAndRotateAsync();

        rotated.Should().BeFalse();
        await _cloudClient.DidNotReceive().RotateApiKeyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRotate_NoCredentials_DoesNotRotate()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns((DeviceCredentials?)null);

        var rotated = await _sut.CheckAndRotateAsync();

        rotated.Should().BeFalse();
    }
}
