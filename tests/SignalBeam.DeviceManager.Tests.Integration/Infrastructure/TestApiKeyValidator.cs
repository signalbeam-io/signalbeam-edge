using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

/// <summary>
/// Test implementation of IApiKeyValidator that accepts any API key for testing.
/// </summary>
public class TestApiKeyValidator : IApiKeyValidator
{
    private readonly Guid _defaultTenantId;

    public TestApiKeyValidator(Guid? defaultTenantId = null)
    {
        _defaultTenantId = defaultTenantId ?? Guid.NewGuid();
    }

    public Task<Result<ApiKeyValidationResult>> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        // For testing, accept any non-empty API key
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(
                Result.Failure<ApiKeyValidationResult>(
                    Error.Validation("INVALID_API_KEY", "API key cannot be empty")));
        }

        var result = new ApiKeyValidationResult
        {
            TenantId = _defaultTenantId.ToString(),
            Scopes = new[] { "devices:read", "devices:write", "groups:read", "groups:write" }
        };

        return Task.FromResult(Result<ApiKeyValidationResult>.Success(result));
    }
}
