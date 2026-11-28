using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.BundleOrchestrator.Host.Endpoints;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Endpoints;

// The rollout, phased-rollout and group-assignment endpoints all map through DependencyErrors.
public class DependencyErrorsTests
{
    [Fact]
    public async Task GroupLookupUnavailable_IsA503()
    {
        // Arrange
        var error = DeviceGroupErrors.From(DeviceGroupLookup.Unavailable, Guid.NewGuid());

        // Act
        var status = await StatusCodeOf(DependencyErrors.ServiceUnavailable(error));

        // Assert
        DependencyErrors.IsUnavailable(error).Should().BeTrue();
        status.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void OtherErrors_AreNotTreatedAsUnavailable()
    {
        DependencyErrors.IsUnavailable(DeviceGroupErrors.From(DeviceGroupLookup.NotFound, Guid.NewGuid())).Should().BeFalse();
        DependencyErrors.IsUnavailable(Error.Failure("STORAGE_FAILED", "Blob upload failed.")).Should().BeFalse();
    }

    private static async Task<int> StatusCodeOf(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        await result.ExecuteAsync(context);
        return context.Response.StatusCode;
    }
}
