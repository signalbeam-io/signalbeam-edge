using Microsoft.AspNetCore.Http;

namespace SignalBeam.Shared.Infrastructure.Http;

/// <summary>
/// Implementation of IHttpContextInfoProvider that extracts information from ASP.NET Core HttpContext.
/// </summary>
public class HttpContextInfoProvider : IHttpContextInfoProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextInfoProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        // Try to get real IP from X-Forwarded-For header (if behind proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Try X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        return context.Request.Headers["User-Agent"].FirstOrDefault();
    }
}
