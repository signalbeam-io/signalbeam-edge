using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.Shared.Infrastructure.InternalServices;

namespace SignalBeam.Shared.Infrastructure.Tests.InternalServices;

public class DeviceGroupClientTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    [Fact]
    public async Task GetAsync_KnownGroup_ReturnsMembersInOrder()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK,
            $"{{\"groupId\":\"{_groupId}\",\"name\":\"canary\",\"deviceIds\":[\"{first}\",\"{second}\"]}}");

        // Act
        var lookup = await Client(handler).GetAsync(_tenantId, _groupId);

        // Assert
        lookup.Status.Should().Be(DeviceGroupLookupStatus.Found);
        lookup.Group!.Name.Should().Be("canary");
        lookup.Group.DeviceIds.Should().Equal(first, second);
        handler.LastPath.Should().Be($"/api/internal/tenants/{_tenantId}/groups/{_groupId}");
    }

    [Fact]
    public async Task GetAsync_UnknownOrOtherTenantsGroup_ReturnsNotFound()
    {
        // Act
        var lookup = await Client(new StubHandler(HttpStatusCode.NotFound, "{}")).GetAsync(_tenantId, _groupId);

        // Assert
        lookup.Should().Be(DeviceGroupLookup.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    [InlineData(HttpStatusCode.OK, "{\"groupId\":\"00000000-0000-0000-0000-000000000000\",\"name\":\"x\",\"deviceIds\":[]}")]
    public async Task GetAsync_LookupFails_ReturnsUnavailable(HttpStatusCode status, string body)
    {
        // Act
        var lookup = await Client(new StubHandler(status, body)).GetAsync(_tenantId, _groupId);

        // Assert
        lookup.Should().Be(DeviceGroupLookup.Unavailable);
    }

    [Fact]
    public async Task GetAsync_DeviceManagerDown_ReturnsUnavailable()
    {
        // Act
        var lookup = await Client(new ThrowingHandler()).GetAsync(_tenantId, _groupId);

        // Assert
        lookup.Should().Be(DeviceGroupLookup.Unavailable);
    }

    private static DeviceGroupClient Client(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://device-manager") },
        NullLogger<DeviceGroupClient>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
