using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Infrastructure.Services;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Tests.Services;

public class DeviceTenantResolverTests
{
    private readonly DeviceId _deviceId = new(Guid.NewGuid());

    [Fact]
    public async Task ResolveAsync_KnownDevice_ReturnsTenantAndCachesIt()
    {
        // Arrange
        var tenant = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, $"{{\"deviceId\":\"{_deviceId.Value}\",\"tenantId\":\"{tenant}\"}}");
        var resolver = Resolver(handler);

        // Act
        var first = await resolver.ResolveAsync(_deviceId);
        var second = await resolver.ResolveAsync(_deviceId);

        // Assert
        first.Should().Be(new TenantId(tenant));
        second.Should().Be(first);
        handler.Calls.Should().Be(1);
        handler.LastPath.Should().Be($"/api/internal/devices/{_deviceId.Value}/tenant");
    }

    [Fact]
    public async Task ResolveAsync_UnknownDevice_ReturnsNullAndCachesTheMiss()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.NotFound, "{}");
        var resolver = Resolver(handler);

        // Act
        var first = await resolver.ResolveAsync(_deviceId);
        await resolver.ResolveAsync(_deviceId);

        // Assert
        first.Should().BeNull();
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ResolveAsync_LookupFails_ReturnsNullWithoutCaching(HttpStatusCode status)
    {
        // Arrange: a misconfigured key or an outage must not hide the tenant once it's fixed.
        var handler = new StubHandler(status, "{}");
        var resolver = Resolver(handler);

        // Act
        await resolver.ResolveAsync(_deviceId);
        var second = await resolver.ResolveAsync(_deviceId);

        // Assert
        second.Should().BeNull();
        handler.Calls.Should().Be(2);
    }

    private static DeviceTenantResolver Resolver(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://device-manager") },
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<DeviceTenantResolver>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
