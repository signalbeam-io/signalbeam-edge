using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Abstraction for validating API keys.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates an API key and returns the associated tenant ID.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the tenant ID if valid, or an error if invalid.</returns>
    Task<Result<ApiKeyValidationResult>> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of API key validation.
/// </summary>
public sealed class ApiKeyValidationResult
{
    /// <summary>
    /// Gets the tenant ID associated with the API key.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the scopes/permissions associated with the API key.
    /// </summary>
    public IReadOnlyCollection<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets additional metadata about the API key.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the expiration time of the API key, if any.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether the API key has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
}
