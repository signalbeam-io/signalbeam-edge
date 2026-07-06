using Microsoft.AspNetCore.Authentication;
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

        // If the request carries a Bearer token, validate it via the JWT scheme and
        // reject it when validation fails. Previously any Bearer value was passed
        // through unvalidated, letting a caller with a bogus token reach endpoints
        // that don't separately enforce authorization (see #422). AuthenticateAsync
        // runs the JWT handler and only sets the principal on success.
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var jwtResult = await context.AuthenticateAsync(AuthenticationConstants.JwtBearerScheme);
            if (jwtResult.Succeeded && jwtResult.Principal is not null)
            {
                AuthMethodClaimStamp.Apply(jwtResult.Principal, AuthenticationConstants.AuthMethodJwt);
                context.User = jwtResult.Principal;
                await _next(context);
                return;
            }

            _logger.LogWarning(
                "Bearer token authentication failed: {Failure}",
                jwtResult.Failure?.Message);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "invalid_token",
                message = "The provided bearer token is invalid or has expired."
            });
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
            new(ClaimTypes.AuthenticationMethod, AuthenticationConstants.ApiKeyScheme),
            // This middleware only validates the config-backed tenant key (no device keys), so the
            // API-key path here is always a tenant key — tag it so the operator policy treats it as
            // the dev-only escape hatch.
            new(AuthenticationConstants.AuthMethodClaimType, AuthenticationConstants.AuthMethodTenantApiKey)
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
