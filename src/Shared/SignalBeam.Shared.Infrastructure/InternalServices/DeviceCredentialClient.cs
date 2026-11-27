using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.InternalServices;

public enum DeviceCredentialStatus
{
    Valid,
    Rejected,
    Unavailable
}

public readonly record struct DeviceCredentialVerification(DeviceCredentialStatus Status, Guid DeviceId, Guid TenantId)
{
    public static DeviceCredentialVerification Valid(Guid deviceId, Guid tenantId) => new(DeviceCredentialStatus.Valid, deviceId, tenantId);
    public static readonly DeviceCredentialVerification Rejected = new(DeviceCredentialStatus.Rejected, Guid.Empty, Guid.Empty);
    public static readonly DeviceCredentialVerification Unavailable = new(DeviceCredentialStatus.Unavailable, Guid.Empty, Guid.Empty);
}

public interface IDeviceCredentialClient
{
    Task<DeviceCredentialVerification> VerifyApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}

// Verifies device API keys against DeviceManager, which owns them. Each check there costs a BCrypt
// hash, so results are cached by a SHA-256 of the key (never the key itself). A valid key stays
// cached briefly, which bounds how long a revoked or rotated key keeps working here; a rejected key
// for less; a failed call barely at all, so an outage clears quickly.
public sealed class DeviceCredentialClient : IDeviceCredentialClient
{
    public static readonly TimeSpan ValidTtl = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan RejectedTtl = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan FailureTtl = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DeviceCredentialClient> _logger;

    public DeviceCredentialClient(HttpClient httpClient, IMemoryCache cache, ILogger<DeviceCredentialClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DeviceCredentialVerification> VerifyApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"device-credential:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))}";
        if (_cache.TryGetValue(cacheKey, out DeviceCredentialVerification cached))
        {
            return cached;
        }

        var verification = await FetchAsync(apiKey, cancellationToken);
        var ttl = verification.Status switch
        {
            DeviceCredentialStatus.Valid => ValidTtl,
            DeviceCredentialStatus.Rejected => RejectedTtl,
            _ => FailureTtl
        };
        _cache.Set(cacheKey, verification, ttl);
        return verification;
    }

    private async Task<DeviceCredentialVerification> FetchAsync(string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"{InternalServiceAuthentication.PathPrefix}/device-credentials/verify",
                new { apiKey },
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                && await IsDeviceKeyRejectionAsync(response, cancellationToken))
            {
                return DeviceCredentialVerification.Rejected;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Device credential verification failed: {StatusCode}", response.StatusCode);
                return DeviceCredentialVerification.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken);
            return body is not null && body.DeviceId != Guid.Empty && body.TenantId != Guid.Empty
                ? DeviceCredentialVerification.Valid(body.DeviceId, body.TenantId)
                : DeviceCredentialVerification.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Device credential verification failed");
            return DeviceCredentialVerification.Unavailable;
        }
    }

    // DeviceManager rejects a device key with a JSON error body, but a missing or wrong
    // InternalServices:ApiKey with a bare 401. Treating the latter as "invalid device key" would make a
    // misconfigured service key look like every device sending a bad key, so it counts as unavailable.
    private static async Task<bool> IsDeviceKeyRejectionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("error", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record VerifyResponse(Guid DeviceId, Guid TenantId);
}

public static class DeviceCredentialClientExtensions
{
    // Registers IDeviceCredentialClient against DeviceManager:BaseUrl, sending InternalServices:ApiKey.
    public static IServiceCollection AddDeviceCredentialClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration[DeviceTenantClient.BaseUrlConfigurationKey] ?? "http://localhost:5296";
        var internalServiceKey = configuration[InternalServiceAuthentication.ConfigurationKey];

        services.AddMemoryCache();
        services.AddHttpClient<IDeviceCredentialClient, DeviceCredentialClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrEmpty(internalServiceKey))
            {
                client.DefaultRequestHeaders.Add(InternalServiceAuthentication.HeaderName, internalServiceKey);
            }
        });

        return services;
    }
}
