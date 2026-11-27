using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.Shared.Infrastructure.InternalServices;

namespace SignalBeam.Shared.Infrastructure.Tests.InternalServices;

public class DeviceCredentialClientTests
{
    private const string ApiKey = "sb_device_abcd1234_secret";

    [Fact]
    public async Task VerifyApiKeyAsync_ValidKey_ReturnsDeviceAndCachesIt()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, $"{{\"deviceId\":\"{deviceId}\",\"tenantId\":\"{tenantId}\"}}");
        var client = Client(handler);

        // Act
        var first = await client.VerifyApiKeyAsync(ApiKey);
        var second = await client.VerifyApiKeyAsync(ApiKey);

        // Assert
        first.Should().Be(DeviceCredentialVerification.Valid(deviceId, tenantId));
        second.Should().Be(first);
        handler.Calls.Should().Be(1);
        handler.LastPath.Should().Be("/api/internal/device-credentials/verify");
        handler.LastBody.Should().Contain(ApiKey, "the key goes in the body, not the URL");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task VerifyApiKeyAsync_DeviceManagerRejectsKey_ReturnsRejectedAndCachesIt(HttpStatusCode status)
    {
        // Arrange
        var handler = new StubHandler(status, "{\"error\":\"INVALID_API_KEY\",\"message\":\"nope\"}");
        var client = Client(handler);

        // Act
        var first = await client.VerifyApiKeyAsync(ApiKey);
        await client.VerifyApiKeyAsync(ApiKey);

        // Assert
        first.Status.Should().Be(DeviceCredentialStatus.Rejected);
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_token\"}")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":42}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task VerifyApiKeyAsync_VerificationFails_ReturnsUnavailable(HttpStatusCode status, string body)
    {
        // Arrange: a bare 401 means our service key was refused, not the device's key.
        var client = Client(new StubHandler(status, body));

        // Act
        var verification = await client.VerifyApiKeyAsync(ApiKey);

        // Assert
        verification.Status.Should().Be(DeviceCredentialStatus.Unavailable);
    }

    [Fact]
    public async Task VerifyApiKeyAsync_DifferentKeys_AreCachedSeparately()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":\"INVALID_API_KEY\"}");
        var client = Client(handler);

        // Act
        await client.VerifyApiKeyAsync(ApiKey);
        await client.VerifyApiKeyAsync(ApiKey + "x");

        // Assert
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public void Ttls_KeepRevokedKeysAndOutagesShortLived()
    {
        DeviceCredentialClient.ValidTtl.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        DeviceCredentialClient.FailureTtl.Should().BeLessThan(DeviceCredentialClient.RejectedTtl);
    }

    private static DeviceCredentialClient Client(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://device-manager") },
        new DeviceCredentialCache(),
        NullLogger<DeviceCredentialClient>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = body.Length == 0 ? new ByteArrayContent([]) : new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
