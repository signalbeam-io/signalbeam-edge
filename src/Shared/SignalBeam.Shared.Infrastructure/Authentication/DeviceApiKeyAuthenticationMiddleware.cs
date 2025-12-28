using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Middleware for device-specific API key authentication.
/// Validates API keys stored in database and checks device approval status.
/// </summary>
public class DeviceApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceApiKeyAuthenticationMiddleware> _logger;

    public DeviceApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<DeviceApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDeviceApiKeyService apiKeyService,
        IDeviceApiKeyValidator validator)
    {
        // Skip authentication for health checks, metrics, and API documentation
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics") ||
            context.Request.Path.StartsWithSegments("/scalar") ||
            context.Request.Path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        // Extract API key from header
        if (!context.Request.Headers.TryGetValue(AuthenticationConstants.ApiKeyHeaderName, out var apiKeyValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "missing_api_key",
                message = $"API key is required in {AuthenticationConstants.ApiKeyHeaderName} header."
            });
            return;
        }

        var apiKey = apiKeyValue.ToString();

        // Extract key prefix for lookup
        var keyPrefix = apiKeyService.ExtractKeyPrefix(apiKey);
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            _logger.LogWarning("Invalid API key format provided");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "invalid_api_key_format",
                message = "The provided API key format is invalid."
            });
            return;
        }

        // Validate API key using database-backed validator
        var validationResult = await validator.ValidateAsync(apiKey, keyPrefix, context.RequestAborted);

        if (validationResult.IsFailure)
        {
            _logger.LogWarning(
                "Device API key validation failed: {ErrorCode} - {ErrorMessage}",
                validationResult.Error!.Code,
                validationResult.Error.Message);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = validationResult.Error.Code,
                message = validationResult.Error.Message
            });
            return;
        }

        // Set user principal with device ID and tenant ID
        var result = validationResult.Value;
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.DeviceIdClaimType, result.DeviceId.ToString()),
            new(AuthenticationConstants.TenantIdClaimType, result.TenantId.ToString()),
            new(ClaimTypes.AuthenticationMethod, AuthenticationConstants.DeviceApiKeyScheme)
        };

        var identity = new ClaimsIdentity(claims, AuthenticationConstants.DeviceApiKeyScheme);
        context.User = new ClaimsPrincipal(identity);

        // Store device and tenant IDs in context for easy access
        context.Items["DeviceId"] = result.DeviceId;
        context.Items["TenantId"] = result.TenantId;

        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding device API key authentication middleware.
/// </summary>
public static class DeviceApiKeyAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds device API key authentication middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseDeviceApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DeviceApiKeyAuthenticationMiddleware>();
    }
}
