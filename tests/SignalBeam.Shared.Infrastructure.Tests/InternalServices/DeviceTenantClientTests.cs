using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.Shared.Infrastructure.InternalServices;

namespace SignalBeam.Shared.Infrastructure.Tests.InternalServices;

public class DeviceTenantClientTests
{
    private readonly Guid _deviceId = Guid.NewGuid();

    [Fact]
    public async Task LookupAsync_KnownDevice_ReturnsTenantAndCachesIt()
    {
        // Arrange
        var tenant = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, $"{{\"deviceId\":\"{_deviceId}\",\"tenantId\":\"{tenant}\"}}");
        var client = Client(handler);

        // Act
        var first = await client.LookupAsync(_deviceId);
        var second = await client.LookupAsync(_deviceId);

        // Assert
        first.Should().Be(DeviceTenantLookup.Found(tenant));
        second.Should().Be(first);
        handler.Calls.Should().Be(1);
        handler.LastPath.Should().Be($"/api/internal/devices/{_deviceId}/tenant");
    }

    [Fact]
    public async Task LookupAsync_UnknownDevice_ReturnsNotFoundAndCachesTheMiss()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.NotFound, "{}");
        var client = Client(handler);

        // Act
        var first = await client.LookupAsync(_deviceId);
        await client.LookupAsync(_deviceId);

        // Assert
        first.Status.Should().Be(DeviceTenantLookupStatus.NotFound);
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    public async Task LookupAsync_LookupFails_ReturnsUnavailable(HttpStatusCode status, string body)
    {
        // Arrange
        var client = Client(new StubHandler(status, body));

        // Act
        var lookup = await client.LookupAsync(_deviceId);

        // Assert
        lookup.Status.Should().Be(DeviceTenantLookupStatus.Unavailable);
    }

    [Fact]
    public async Task LookupAsync_LookupFails_CachesOnlyBriefly()
    {
        // Arrange: an outage shouldn't mean one call per device per caller, nor hide the tenant for long.
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = Client(handler, cache);

        // Act
        await client.LookupAsync(_deviceId);
        await client.LookupAsync(_deviceId);

        // Assert
        handler.Calls.Should().Be(1);
        DeviceTenantClient.FailureTtl.Should().BeLessThan(DeviceTenantClient.MissTtl);
    }

    private static DeviceTenantClient Client(StubHandler handler, IMemoryCache? cache = null) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://device-manager") },
        cache ?? new MemoryCache(new MemoryCacheOptions()),
        NullLogger<DeviceTenantClient>.Instance);

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
