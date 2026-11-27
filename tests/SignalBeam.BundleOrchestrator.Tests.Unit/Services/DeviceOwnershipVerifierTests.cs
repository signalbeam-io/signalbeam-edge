using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Services;

public class DeviceOwnershipVerifierTests
{
    private readonly IDeviceTenantClient _tenantClient = Substitute.For<IDeviceTenantClient>();
    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DeviceOwnershipVerifier _verifier;

    public DeviceOwnershipVerifierTests()
    {
        _verifier = new DeviceOwnershipVerifier(_tenantClient);
        _tenantClient.LookupAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(DeviceTenantLookup.Found(_tenantId));
    }

    [Fact]
    public async Task CheckAsync_DeviceItself_PassesWithoutLookup()
    {
        (await _verifier.CheckAsync(_deviceId, new DeviceCaller(_tenantId, _deviceId), CancellationToken.None)).Should().BeNull();
        await _tenantClient.DidNotReceive().LookupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_AnotherDevice_ReturnsNotFound()
    {
        var error = await _verifier.CheckAsync(_deviceId, new DeviceCaller(_tenantId, Guid.NewGuid()), CancellationToken.None);
        error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task CheckAsync_OperatorOfTheDevicesTenant_Passes()
    {
        (await _verifier.CheckAsync(_deviceId, new DeviceCaller(_tenantId, null), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_OperatorOfAnotherTenant_ReturnsNotFound()
    {
        var error = await _verifier.CheckAsync(_deviceId, new DeviceCaller(Guid.NewGuid(), null), CancellationToken.None);
        error!.Code.Should().Be("DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task CheckAsync_CallerWithoutTenant_ReturnsForbidden()
    {
        var error = await _verifier.CheckAsync(_deviceId, new DeviceCaller(null, null), CancellationToken.None);
        error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task CheckAsync_LookupUnavailable_ReturnsFailure()
    {
        _tenantClient.LookupAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(DeviceTenantLookup.Unavailable);

        var error = await _verifier.CheckAsync(_deviceId, new DeviceCaller(_tenantId, null), CancellationToken.None);

        error!.Type.Should().Be(ErrorType.Failure);
    }
}
