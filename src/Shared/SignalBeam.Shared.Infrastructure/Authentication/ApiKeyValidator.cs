using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Simple in-memory API key validator for MVP.
/// In production, this should be replaced with database-backed validation.
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyValidator> _logger;

    public ApiKeyValidator(
        IConfiguration configuration,
        ILogger<ApiKeyValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<Result<ApiKeyValidationResult>> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var error = Error.Validation(
                "INVALID_API_KEY",
                "API key cannot be empty.");
            return Task.FromResult(Result.Failure<ApiKeyValidationResult>(error));
        }

        // For MVP: Check against configured valid API keys
        // Format in appsettings.json: "Authentication:ApiKeys:0": "tenant-id:api-key:scopes"
        var apiKeys = _configuration.GetSection("Authentication:ApiKeys").Get<string[]>() ?? Array.Empty<string>();

        foreach (var configuredKey in apiKeys)
        {
            var parts = configuredKey.Split(':', 3);
            if (parts.Length < 2)
            {
                _logger.LogWarning("Invalid API key configuration format: {ConfiguredKey}", configuredKey);
                continue;
            }

            var tenantId = parts[0];
            var key = parts[1];
            var scopes = parts.Length > 2 ? parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();

            if (key == apiKey)
            {
                _logger.LogInformation("API key validated successfully for tenant {TenantId}", tenantId);

                var result = new ApiKeyValidationResult
                {
                    TenantId = tenantId,
                    Scopes = scopes,
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = "configuration"
                    }
                };

                return Task.FromResult(Result<ApiKeyValidationResult>.Success(result));
            }
        }

        _logger.LogWarning("API key validation failed: key not found");

        var notFoundError = Error.Unauthorized(
            "INVALID_API_KEY",
            "The provided API key is not valid.");
        return Task.FromResult(Result.Failure<ApiKeyValidationResult>(notFoundError));
    }
}
