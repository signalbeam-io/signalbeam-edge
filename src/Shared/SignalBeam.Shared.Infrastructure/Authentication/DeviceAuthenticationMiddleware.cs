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
        IDeviceApiKeyValidator? apiKeyValidator = null,
        IApiKeyValidator? tenantApiKeyValidator = null)
    {
        // Skip authentication for health checks, metrics, and API documentation
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics") ||
            context.Request.Path.StartsWithSegments("/scalar") ||
            context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/api/certificates/ca")) // CA cert is public
        {
            await _next(context);
            return;
        }

        // Skip device-key auth for the registration handshake. These endpoints run before the
        // device holds an API key and are each authenticated by other means in their handlers:
        // register and claim-key verify the registration token; registration-status returns no secrets.
        if (IsRegistrationHandshake(context.Request))
        {
            await _next(context);
            return;
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

        // [2] If request has a Bearer token, skip this middleware and let JWT auth handle it
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // [3] Fallback to API key authentication
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

        // [2a] Try device-specific API key first (new format: sb_device_{prefix}_{secret})
        if (apiKeyService != null && apiKeyValidator != null)
        {
            var keyPrefix = apiKeyService.ExtractKeyPrefix(apiKey);
            if (!string.IsNullOrWhiteSpace(keyPrefix))
            {
                // This looks like a device-specific API key
                var apiKeyResult = await apiKeyValidator.ValidateAsync(
                    apiKey,
                    keyPrefix,
                    context.RequestAborted);

                if (apiKeyResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "Device {DeviceId} authenticated via device-specific API key",
                        apiKeyResult.Value.DeviceId);

                    SetUserPrincipal(context, apiKeyResult.Value, AuthenticationMethod.ApiKey);
                    await _next(context);
                    return;
                }

                _logger.LogDebug(
                    "Device-specific API key validation failed: {ErrorCode} - {ErrorMessage}",
                    apiKeyResult.Error!.Code,
                    apiKeyResult.Error.Message);
            }
        }

        // [2b] Fallback to tenant-level API key (old format: {tenantId}:{key}:{scopes})
        if (tenantApiKeyValidator != null)
        {
            _logger.LogDebug("Attempting tenant-level API key authentication");

            var tenantKeyResult = await tenantApiKeyValidator.ValidateAsync(
                apiKey,
                context.RequestAborted);

            if (tenantKeyResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Tenant {TenantId} authenticated via tenant-level API key",
                    tenantKeyResult.Value.TenantId);

                SetTenantPrincipal(context, tenantKeyResult.Value);
                await _next(context);
                return;
            }

            _logger.LogDebug(
                "Tenant-level API key validation failed");
        }

        // Both authentication methods failed
        _logger.LogWarning("All API key authentication methods failed");
        await RespondUnauthorized(context,
            "INVALID_API_KEY",
            "The provided API key is invalid or has expired.");
    }

    /// <summary>
    /// Identifies the registration-handshake endpoints that must be reachable before a device
    /// has an API key: device registration, approval-status polling, and the one-time key claim.
    /// Uses exact method + path matching to avoid exposing other device endpoints.
    /// </summary>
    private static bool IsRegistrationHandshake(HttpRequest request)
    {
        var path = (request.Path.Value ?? string.Empty).Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 ||
            !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals("devices", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // POST /api/devices — device self-registration (registration token verified in handler)
        if (segments.Length == 2)
        {
            return HttpMethods.IsPost(request.Method);
        }

        // /api/devices/{guid}/{sub} — only the registration-status and claim-key sub-resources.
        // Requiring a parseable GUID segment prevents an artificial path from matching by suffix.
        if (segments.Length == 4 && Guid.TryParse(segments[2], out _))
        {
            return segments[3] switch
            {
                _ when segments[3].Equals("registration-status", StringComparison.OrdinalIgnoreCase)
                    => HttpMethods.IsGet(request.Method), // status only, returns no secrets
                _ when segments[3].Equals("claim-key", StringComparison.OrdinalIgnoreCase)
                    => HttpMethods.IsPost(request.Method), // one-time key claim, token verified in handler
                _ => false
            };
        }

        return false;
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

    private void SetTenantPrincipal(
        HttpContext context,
        ApiKeyValidationResult result)
    {
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.TenantIdClaimType, result.TenantId),
            new(ClaimTypes.AuthenticationMethod, "TenantApiKey")
        };

        // Add scopes as claims
        foreach (var scope in result.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationConstants.ApiKeyScheme);
        context.User = new ClaimsPrincipal(identity);

        // Store tenant ID in context for easy access
        context.Items["TenantId"] = result.TenantId;
        context.Items["AuthenticationMethod"] = "TenantApiKey";
        context.Items["Scopes"] = result.Scopes;
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
