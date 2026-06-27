using System.Net;
using Polly;
using Polly.Timeout;
using SignalBeam.DeviceManager.Infrastructure.ExternalServices;

namespace SignalBeam.DeviceManager.Tests.Unit.ExternalServices;

public class QuotaCheckResilienceTests
{
    // Near-zero backoff so the retry tests run fast.
    private static readonly QuotaCheckResilienceOptions FastOptions =
        new(MaxRetryAttempts: 3, BaseDelay: TimeSpan.FromMilliseconds(1), PerAttemptTimeout: TimeSpan.FromSeconds(5));

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        QuotaCheckResilience.Configure(builder, FastOptions);
        return builder.Build();
    }

    [Fact]
    public async Task RetriesAndSucceeds_WhenIdentityManagerColdStarts()
    {
        // Simulates IdentityManager being scale-to-zero: the first attempt times out while the
        // container cold-starts, the retry hits it warm and succeeds (instead of QUOTA_CHECK_ERROR).
        var pipeline = BuildPipeline();
        var attempts = 0;

        var response = await pipeline.ExecuteAsync<HttpResponseMessage>(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TimeoutRejectedException("cold start");
            }

            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        attempts.Should().Be(2);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RetriesOnTransientServerError_ThenSucceeds()
    {
        var pipeline = BuildPipeline();
        var attempts = 0;

        var response = await pipeline.ExecuteAsync<HttpResponseMessage>(_ =>
        {
            attempts++;
            var status = attempts < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return ValueTask.FromResult(new HttpResponseMessage(status));
        });

        attempts.Should().Be(3);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DoesNotRetry_OnSuccess()
    {
        var pipeline = BuildPipeline();
        var attempts = 0;

        var response = await pipeline.ExecuteAsync<HttpResponseMessage>(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        attempts.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DoesNotRetry_OnClientError()
    {
        // A 4xx (quota exceeded / tenant not found) is a definitive answer, not a transient failure.
        var pipeline = BuildPipeline();
        var attempts = 0;

        var response = await pipeline.ExecuteAsync<HttpResponseMessage>(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        attempts.Should().Be(1);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
