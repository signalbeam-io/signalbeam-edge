using System.Security.Claims;
using SignalBeam.BundleOrchestrator.Host.Endpoints;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Endpoints;

public class BundleAssignmentEndpointsTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    [Theory]
    [InlineData(AuthenticationConstants.AuthMethodDeviceApiKey)]
    [InlineData(AuthenticationConstants.AuthMethodCertificate)]
    public void CallerFrom_DeviceCredential_KeepsDeviceId(string authMethod)
    {
        var caller = BundleAssignmentEndpoints.CallerFrom(Principal(authMethod));

        caller.DeviceId.Should().Be(_deviceId);
        caller.TenantId.Should().Be(_tenantId);
    }

    [Theory]
    [InlineData(AuthenticationConstants.AuthMethodJwt)]
    [InlineData(AuthenticationConstants.AuthMethodTenantApiKey)]
    public void CallerFrom_NonDeviceCredential_IgnoresDeviceIdClaim(string authMethod)
    {
        // A JWT with its own device_id claim must not skip the tenant check.
        var caller = BundleAssignmentEndpoints.CallerFrom(Principal(authMethod));

        caller.DeviceId.Should().BeNull();
        caller.TenantId.Should().Be(_tenantId);
    }

    private ClaimsPrincipal Principal(string authMethod) => new(new ClaimsIdentity(
        [
            new Claim(AuthenticationConstants.AuthMethodClaimType, authMethod),
            new Claim(AuthenticationConstants.TenantIdClaimType, _tenantId.ToString()),
            new Claim(AuthenticationConstants.DeviceIdClaimType, _deviceId.ToString())
        ],
        "test"));
}
