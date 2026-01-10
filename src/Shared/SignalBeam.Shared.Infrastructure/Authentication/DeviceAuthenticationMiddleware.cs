using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Enums;
using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Unified middleware for device authentication supporting both mTLS and API keys.
/// Certificate authentication takes precedence over API key authentication.
/// Falls back to API key if no certificate is present.
/// </summary>
public class DeviceAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceAuthenticationMiddleware> _logger;

    public DeviceAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<DeviceAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IDeviceCertificateValidator? certificateValidator = null,
        IDeviceApiKeyService? apiKeyService = null,
        IDeviceApiKeyValidator? apiKeyValidator = null)
    {
        // Skip authentication for health checks, metrics, and API documentation
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics") ||
            context.Request.Path.StartsWithSegments("/scalar") ||
            context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/api/certificates/ca") || // CA cert is public
            context.Request.Path.StartsWithSegments("/api/registration-tokens")) // Registration tokens (admin only in production)
        {
            await _next(context);
            return;
        }

        // Skip authentication for device registration when using registration token
        // POST /api/devices with X-Registration-Token header
        if (context.Request.Method == HttpMethods.Post &&
            context.Request.Path.Equals("/api/devices") &&
            context.Request.Headers.ContainsKey("X-Registration-Token"))
        {
            _logger.LogDebug("Device registration with token detected, skipping device authentication");
            await _next(context);
            return;
        }

        // Skip authentication for device approval/rejection (admin only in production)
        // These endpoints require admin auth in production but are open for dev testing
        if (context.Request.Method == HttpMethods.Post &&
            (context.Request.Path.Value?.Contains("/approve") == true ||
             context.Request.Path.Value?.Contains("/reject") == true))
        {
            _logger.LogDebug("Device approval/rejection endpoint detected, skipping device authentication for development");
            await _next(context);
            return;
        }

        // Allow static development API keys (for UI/admin access during development)
        // In production, these should be validated through proper admin authentication
        if (context.Request.Headers.TryGetValue(AuthenticationConstants.ApiKeyHeaderName, out var devApiKey))
        {
            var key = devApiKey.ToString();
            if (key == "dev-api-key-1" || key == "dev-api-key-2")
            {
                _logger.LogDebug("Development API key detected, allowing request");
                await _next(context);
                return;
            }
        }

        // [1] Try certificate authentication first (if mTLS is configured)
        var clientCert = context.Connection.ClientCertificate;
        if (clientCert != null && certificateValidator != null)
        {
            _logger.LogDebug("Client certificate present. Attempting certificate authentication.");

            var certResult = await certificateValidator.ValidateAsync(
                clientCert,
                context.RequestAborted);

            if (certResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Device {DeviceId} authenticated via certificate",
                    certResult.Value.DeviceId);

                SetUserPrincipal(context, certResult.Value, AuthenticationMethod.Certificate);
                await _next(context);
                return;
            }

            _logger.LogWarning(
                "Certificate validation failed: {Error}. Falling back to API key authentication.",
                certResult.Error?.Message);
        }

        // [2] Fallback to API key authentication
        if (!context.Request.Headers.TryGetValue(
            AuthenticationConstants.ApiKeyHeaderName,
            out var apiKeyValue))
        {
            _logger.LogWarning("No client certificate and no API key provided");
            await RespondUnauthorized(context,
                "MISSING_CREDENTIALS",
                "Either a valid client certificate or API key is required.");
            return;
        }

        var apiKey = apiKeyValue.ToString();

        // Validate API key format and extract prefix
        if (apiKeyService == null || apiKeyValidator == null)
        {
            _logger.LogError("API key services not configured");
            await RespondUnauthorized(context,
                "AUTHENTICATION_NOT_CONFIGURED",
                "API key authentication is not properly configured.");
            return;
        }

        var keyPrefix = apiKeyService.ExtractKeyPrefix(apiKey);
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            _logger.LogWarning("Invalid API key format provided");
            await RespondUnauthorized(context,
                "INVALID_API_KEY_FORMAT",
                "The provided API key format is invalid.");
            return;
        }

        // Validate API key
        var apiKeyResult = await apiKeyValidator.ValidateAsync(
            apiKey,
            keyPrefix,
            context.RequestAborted);

        if (apiKeyResult.IsFailure)
        {
            _logger.LogWarning(
                "API key validation failed: {ErrorCode} - {ErrorMessage}",
                apiKeyResult.Error!.Code,
                apiKeyResult.Error.Message);

            await RespondUnauthorized(context,
                apiKeyResult.Error.Code,
                apiKeyResult.Error.Message);
            return;
        }

        _logger.LogInformation(
            "Device {DeviceId} authenticated via API key",
            apiKeyResult.Value.DeviceId);

        SetUserPrincipal(context, apiKeyResult.Value, AuthenticationMethod.ApiKey);
        await _next(context);
    }

    private void SetUserPrincipal(
        HttpContext context,
        dynamic result, // Can be from either validator
        AuthenticationMethod method)
    {
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.DeviceIdClaimType, result.DeviceId.ToString()),
            new(AuthenticationConstants.TenantIdClaimType, result.TenantId.ToString()),
            new(ClaimTypes.AuthenticationMethod, method.ToString())
        };

        var schemeName = method == AuthenticationMethod.Certificate
            ? AuthenticationConstants.CertificateScheme
            : AuthenticationConstants.DeviceApiKeyScheme;

        var identity = new ClaimsIdentity(claims, schemeName);
        context.User = new ClaimsPrincipal(identity);

        // Store device and tenant IDs in context for easy access
        context.Items["DeviceId"] = result.DeviceId;
        context.Items["TenantId"] = result.TenantId;
        context.Items["AuthenticationMethod"] = method;
    }

    private async Task RespondUnauthorized(
        HttpContext context,
        string errorCode,
        string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            error = errorCode,
            message = message
        });
    }
}

/// <summary>
/// Extension methods for adding unified device authentication middleware.
/// </summary>
public static class DeviceAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds unified device authentication middleware (supports both mTLS and API keys).
    /// This should replace UseDeviceApiKeyAuthentication() when enabling mTLS.
    /// </summary>
    public static IApplicationBuilder UseDeviceAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DeviceAuthenticationMiddleware>();
    }
}
