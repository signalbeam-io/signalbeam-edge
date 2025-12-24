using Microsoft.AspNetCore.Http;
using SignalBeam.BundleOrchestrator.Host.Endpoints;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Endpoints;

public class BundleVersionEndpointsTests
{
    [Fact]
    public void TryHandleBundleDefinitionCaching_Returns304WhenEtagMatches()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["If-None-Match"] = "\"sha256:abc\"";

        var handled = BundleVersionEndpoints.TryHandleBundleDefinitionCaching(
            context.Request,
            context.Response,
            "sha256:abc",
            DateTimeOffset.UtcNow,
            out var result);

        handled.Should().BeTrue();
        result.Should().NotBeNull();
        context.Response.Headers["ETag"].ToString().Should().Be("\"sha256:abc\"");
    }

    [Fact]
    public void TryHandleBundleDefinitionCaching_SetsHeadersWhenEtagDoesNotMatch()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["If-None-Match"] = "\"sha256:other\"";
        var createdAt = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var handled = BundleVersionEndpoints.TryHandleBundleDefinitionCaching(
            context.Request,
            context.Response,
            "sha256:abc",
            createdAt,
            out var result);

        handled.Should().BeFalse();
        result.Should().BeNull();
        context.Response.Headers["ETag"].ToString().Should().Be("\"sha256:abc\"");
        context.Response.Headers["Cache-Control"].ToString().Should().Be("public, max-age=300");
        context.Response.Headers["Last-Modified"].ToString().Should().Be(createdAt.ToUniversalTime().ToString("R"));
    }
}
