using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SignalBeam.Shared.Infrastructure.Authentication;

// Service-to-service calls (e.g. TelemetryProcessor asking DeviceManager for a device's tenant)
// carry a shared key. Internal endpoints live under /api/internal, which the gateway does not
// route, and refuse every call when no key is configured (fail closed).
public static class InternalServiceAuthentication
{
    public const string HeaderName = "X-Internal-Service-Key";
    public const string ConfigurationKey = "InternalServices:ApiKey";
    public const string PathPrefix = "/api/internal";

    public static TBuilder RequireInternalServiceKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = configuration[ConfigurationKey];
            var presented = context.HttpContext.Request.Headers[HeaderName].ToString();

            return IsValid(expected, presented) ? await next(context) : Microsoft.AspNetCore.Http.Results.Unauthorized();
        });

        return builder;
    }

    public static bool IsValid(string? expected, string? presented)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(presented))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented));
    }
}
