using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace SignalBeam.DeviceManager.Infrastructure.ExternalServices;

/// <summary>
/// Resilience options for the IdentityManager device-quota check. IdentityManager is scale-to-zero
/// in the cloud, so the first quota check after it idles can exceed a single attempt's timeout while
/// the container cold-starts. Retrying on timeouts and transient failures lets the first attempt wake
/// it and a later attempt hit it warm — instead of failing device registration with QUOTA_CHECK_ERROR.
/// </summary>
public sealed record QuotaCheckResilienceOptions(
    int MaxRetryAttempts,
    TimeSpan BaseDelay,
    TimeSpan PerAttemptTimeout)
{
    public const string SectionName = "IdentityManager:QuotaCheck";

    public static QuotaCheckResilienceOptions Default { get; } =
        new(MaxRetryAttempts: 3, BaseDelay: TimeSpan.FromSeconds(2), PerAttemptTimeout: TimeSpan.FromSeconds(15));

    public static QuotaCheckResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new QuotaCheckResilienceOptions(
            MaxRetryAttempts: section.GetValue("MaxRetryAttempts", Default.MaxRetryAttempts),
            BaseDelay: TimeSpan.FromSeconds(section.GetValue("BaseDelaySeconds", Default.BaseDelay.TotalSeconds)),
            PerAttemptTimeout: TimeSpan.FromSeconds(section.GetValue("PerAttemptTimeoutSeconds", Default.PerAttemptTimeout.TotalSeconds)));
    }
}

/// <summary>
/// Builds the resilience pipeline for the IdentityManager quota-check HTTP client. Extracted so the
/// retry-on-cold-start behaviour can be unit tested independently of the full DI graph.
/// </summary>
public static class QuotaCheckResilience
{
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        QuotaCheckResilienceOptions options)
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = options.BaseDelay,
            ShouldHandle = args => args.Outcome switch
            {
                { Exception: HttpRequestException } => PredicateResult.True(),
                { Exception: TimeoutRejectedException } => PredicateResult.True(),
                { Result: { } response } when (int)response.StatusCode >= 500 => PredicateResult.True(),
                _ => PredicateResult.False()
            }
        });

        // Per-attempt timeout — long enough to wake a cold container on a retry.
        builder.AddTimeout(options.PerAttemptTimeout);
    }
}
