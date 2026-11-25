using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using SignalBeam.EdgeAgent.Simulator;

namespace SignalBeam.EdgeAgent.Tests.Unit.Simulator;

public class FleetSetupRetryTests
{
    [Theory]
    [InlineData("{\"error\":\"RATE_LIMIT_EXCEEDED\",\"retryAfter\":12.3}", 13)]
    [InlineData("{\"retryAfter\":0.2}", 1)]
    [InlineData("{\"retryAfter\":600}", 60)]
    public async Task GetRetryAfterAsync_NumericBody_UsesBodyValueWithinBounds(string body, int expectedSeconds)
    {
        // Arrange
        using var response = TooManyRequests(body);

        // Act
        var wait = await FleetSetup.GetRetryAfterAsync(response, CancellationToken.None);

        // Assert
        wait.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    // DeviceManager sends retryAfter: null when the limiter has no RetryAfter metadata.
    [InlineData("{\"error\":\"RATE_LIMIT_EXCEEDED\",\"retryAfter\":null}")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[1,2]")]
    public async Task GetRetryAfterAsync_NoUsableBody_FallsBackToHeader(string body)
    {
        // Arrange
        using var response = TooManyRequests(body);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        // Act
        var wait = await FleetSetup.GetRetryAfterAsync(response, CancellationToken.None);

        // Assert
        wait.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task GetRetryAfterAsync_NoBodyValueOrHeader_UsesDefault()
    {
        // Arrange
        using var response = TooManyRequests("{\"retryAfter\":null}");

        // Act
        var wait = await FleetSetup.GetRetryAfterAsync(response, CancellationToken.None);

        // Assert
        wait.Should().Be(FleetSetup.DefaultRetryDelay);
    }

    [Fact]
    public async Task SendWithRateLimitRetryAsync_RetriesUntilNotRateLimited()
    {
        // Arrange
        var responses = new Queue<HttpResponseMessage>(
        [
            TooManyRequests("{\"retryAfter\":3}"),
            TooManyRequests("{\"retryAfter\":null}"),
            new HttpResponseMessage(HttpStatusCode.Created),
        ]);
        var sends = 0;
        var waits = new List<TimeSpan>();

        // Act
        using var result = await FleetSetup.SendWithRateLimitRetryAsync(
            () => { sends++; return Task.FromResult(responses.Dequeue()); },
            "register",
            CancellationToken.None,
            (wait, _) => { waits.Add(wait); return Task.CompletedTask; });

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        sends.Should().Be(3);
        waits.Should().Equal(TimeSpan.FromSeconds(3), FleetSetup.DefaultRetryDelay);
    }

    [Fact]
    public async Task SendWithRateLimitRetryAsync_AlwaysRateLimited_ReturnsLastResponseAfterMaxAttempts()
    {
        // Arrange
        var sends = 0;

        // Act
        using var result = await FleetSetup.SendWithRateLimitRetryAsync(
            () => { sends++; return Task.FromResult(TooManyRequests("{\"retryAfter\":1}")); },
            "register",
            CancellationToken.None,
            (_, _) => Task.CompletedTask);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        sends.Should().Be(FleetSetup.MaxAttempts);
    }

    [Fact]
    public async Task SendWithRateLimitRetryAsync_OtherErrors_AreNotRetried()
    {
        // Arrange
        var sends = 0;

        // Act
        using var result = await FleetSetup.SendWithRateLimitRetryAsync(
            () => { sends++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)); },
            "register",
            CancellationToken.None,
            (_, _) => throw new InvalidOperationException("must not wait"));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sends.Should().Be(1);
    }

    private static HttpResponseMessage TooManyRequests(string body) =>
        new(HttpStatusCode.TooManyRequests) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
