using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Middleware for API key authentication.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IApiKeyValidator _validator;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IApiKeyValidator validator,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _validator = validator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
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

        // Validate API key
        var validationResult = await _validator.ValidateAsync(apiKey, context.RequestAborted);

        if (validationResult.IsFailure)
        {
            _logger.LogWarning(
                "API key validation failed: {ErrorCode} - {ErrorMessage}",
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

        // Set user principal with tenant ID and scopes
        var result = validationResult.Value;
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.TenantIdClaimType, result.TenantId),
            new(ClaimTypes.AuthenticationMethod, AuthenticationConstants.ApiKeyScheme)
        };

        foreach (var scope in result.Scopes)
        {
            claims.Add(new Claim(AuthenticationConstants.ScopeClaimType, scope));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationConstants.ApiKeyScheme);
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding API key authentication middleware.
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
