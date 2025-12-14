using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SignalBeam.Shared.Infrastructure.Resilience;

/// <summary>
/// Registry for storing and retrieving resilience policies.
/// </summary>
public interface IResiliencePolicyRegistry
{
    /// <summary>
    /// Gets a policy by name.
    /// </summary>
    IAsyncPolicy? GetPolicy(string policyName);

    /// <summary>
    /// Gets a typed policy by name.
    /// </summary>
    IAsyncPolicy<TResult>? GetPolicy<TResult>(string policyName);

    /// <summary>
    /// Registers a policy with a name.
    /// </summary>
    void RegisterPolicy(string policyName, IAsyncPolicy policy);

    /// <summary>
    /// Registers a typed policy with a name.
    /// </summary>
    void RegisterPolicy<TResult>(string policyName, IAsyncPolicy<TResult> policy);
}

/// <summary>
/// Default implementation of resilience policy registry.
/// </summary>
public sealed class ResiliencePolicyRegistry : IResiliencePolicyRegistry
{
    private readonly Dictionary<string, IAsyncPolicy> _policies = new();
    private readonly Dictionary<string, object> _typedPolicies = new();

    public IAsyncPolicy? GetPolicy(string policyName)
    {
        return _policies.TryGetValue(policyName, out var policy) ? policy : null;
    }

    public IAsyncPolicy<TResult>? GetPolicy<TResult>(string policyName)
    {
        return _typedPolicies.TryGetValue(policyName, out var policy)
            ? policy as IAsyncPolicy<TResult>
            : null;
    }

    public void RegisterPolicy(string policyName, IAsyncPolicy policy)
    {
        _policies[policyName] = policy;
    }

    public void RegisterPolicy<TResult>(string policyName, IAsyncPolicy<TResult> policy)
    {
        _typedPolicies[policyName] = policy;
    }
}

/// <summary>
/// Well-known policy names.
/// </summary>
public static class PolicyNames
{
    public const string DatabaseRetry = "database-retry";
    public const string HttpRetry = "http-retry";
    public const string MessageBrokerRetry = "message-broker-retry";
    public const string CircuitBreaker = "circuit-breaker";
    public const string Timeout = "timeout";
    public const string Combined = "combined";
}
