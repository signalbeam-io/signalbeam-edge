using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Commands;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Tests.Unit.Commands;

public class CheckRegistrationStatusCommandHandlerTests
{
    private readonly ICloudClient _cloudClient = Substitute.For<ICloudClient>();
    private readonly IDeviceCredentialsStore _store = Substitute.For<IDeviceCredentialsStore>();
    private readonly CheckRegistrationStatusCommandHandler _handler;

    private readonly Guid _deviceId = Guid.NewGuid();

    public CheckRegistrationStatusCommandHandlerTests()
    {
        var logger = Substitute.For<ILogger<CheckRegistrationStatusCommandHandler>>();
        _handler = new CheckRegistrationStatusCommandHandler(_cloudClient, _store, logger);
    }

    private DeviceCredentials PendingCredentials(string? token = "sbt_abc12345_secret") => new()
    {
        DeviceId = _deviceId,
        RegistrationStatus = "Pending",
        ApiKey = null,
        RegistrationToken = token
    };

    [Fact]
    public async Task Handle_ApprovedWithClaimAvailable_ClaimsAndStoresKey()
    {
        var credentials = PendingCredentials();
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(credentials);
        _cloudClient.CheckRegistrationStatusAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new RegistrationStatusResponse("Approved", ApiKey: null, KeyClaimAvailable: true));
        var expiry = DateTimeOffset.UtcNow.AddDays(90);
        _cloudClient.ClaimApiKeyAsync(_deviceId, "sbt_abc12345_secret", Arg.Any<CancellationToken>())
            .Returns(new ClaimedApiKey("sb_device_ab12_secret", expiry));

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().Be("sb_device_ab12_secret");
        result.Value.IsApproved.Should().BeTrue();
        await _cloudClient.Received(1).ClaimApiKeyAsync(_deviceId, "sbt_abc12345_secret", Arg.Any<CancellationToken>());
        await _store.Received().SaveCredentialsAsync(
            Arg.Is<DeviceCredentials>(c => c.ApiKey == "sb_device_ab12_secret"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApprovedButClaimNotAvailable_DoesNotClaim()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(PendingCredentials());
        _cloudClient.CheckRegistrationStatusAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new RegistrationStatusResponse("Approved", ApiKey: null, KeyClaimAvailable: false));

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().BeNull();
        await _cloudClient.DidNotReceive().ClaimApiKeyAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClaimAvailableButNoToken_DoesNotClaim()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(PendingCredentials(token: null));
        _cloudClient.CheckRegistrationStatusAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new RegistrationStatusResponse("Approved", ApiKey: null, KeyClaimAvailable: true));

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _cloudClient.DidNotReceive().ClaimApiKeyAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyApprovedWithLocalKey_ShortCircuits()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(new DeviceCredentials
        {
            DeviceId = _deviceId,
            RegistrationStatus = "Approved",
            ApiKey = "sb_device_existing"
        });

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().Be("sb_device_existing");
        await _cloudClient.DidNotReceive().CheckRegistrationStatusAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotRegistered_ReturnsFailure()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns((DeviceCredentials?)null);

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ClaimThrows_DoesNotFailPoll()
    {
        _store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(PendingCredentials());
        _cloudClient.CheckRegistrationStatusAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new RegistrationStatusResponse("Approved", ApiKey: null, KeyClaimAvailable: true));
        _cloudClient.ClaimApiKeyAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaimedApiKey>(_ => throw new HttpRequestException("boom"));

        var result = await _handler.Handle(new CheckRegistrationStatusCommand(), CancellationToken.None);

        // Transient claim failure must not fail the poll — next cycle retries.
        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiKey.Should().BeNull();
    }
}
