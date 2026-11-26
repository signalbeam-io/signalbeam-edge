using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.InternalServices;

public enum DeviceTenantLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

public readonly record struct DeviceTenantLookup(DeviceTenantLookupStatus Status, Guid TenantId)
{
    public static DeviceTenantLookup Found(Guid tenantId) => new(DeviceTenantLookupStatus.Found, tenantId);
    public static readonly DeviceTenantLookup NotFound = new(DeviceTenantLookupStatus.NotFound, Guid.Empty);
    public static readonly DeviceTenantLookup Unavailable = new(DeviceTenantLookupStatus.Unavailable, Guid.Empty);
}

public interface IDeviceTenantClient
{
    Task<DeviceTenantLookup> LookupAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

// Looks up a device's tenant from DeviceManager's internal endpoint. A device never changes tenant,
// so hits are cached for long; misses briefly, so a newly registered device shows up soon; failures
// very briefly, so an outage doesn't turn into one slow call per device per caller.
public sealed class DeviceTenantClient : IDeviceTenantClient
{
    public const string BaseUrlConfigurationKey = "DeviceManager:BaseUrl";

    public static readonly TimeSpan HitTtl = TimeSpan.FromHours(6);
    public static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan FailureTtl = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DeviceTenantClient> _logger;

    public DeviceTenantClient(HttpClient httpClient, IMemoryCache cache, ILogger<DeviceTenantClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DeviceTenantLookup> LookupAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"device-tenant:{deviceId}";
        if (_cache.TryGetValue(cacheKey, out DeviceTenantLookup cached))
        {
            return cached;
        }

        var lookup = await FetchAsync(deviceId, cancellationToken);
        var ttl = lookup.Status switch
        {
            DeviceTenantLookupStatus.Found => HitTtl,
            DeviceTenantLookupStatus.NotFound => MissTtl,
            _ => FailureTtl
        };
        _cache.Set(cacheKey, lookup, ttl);
        return lookup;
    }

    private async Task<DeviceTenantLookup> FetchAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{InternalServiceAuthentication.PathPrefix}/devices/{deviceId}/tenant", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return DeviceTenantLookup.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                // 401: InternalServices:ApiKey missing or different from DeviceManager's.
                _logger.LogWarning("Device tenant lookup for {DeviceId} failed: {StatusCode}", deviceId, response.StatusCode);
                return DeviceTenantLookup.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<DeviceTenantResponse>(cancellationToken);
            return body is not null && body.TenantId != Guid.Empty
                ? DeviceTenantLookup.Found(body.TenantId)
                : DeviceTenantLookup.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Device tenant lookup for {DeviceId} failed", deviceId);
            return DeviceTenantLookup.Unavailable;
        }
    }

    private sealed record DeviceTenantResponse(Guid DeviceId, Guid TenantId);
}

public static class DeviceTenantClientExtensions
{
    // Registers IDeviceTenantClient against DeviceManager:BaseUrl, sending InternalServices:ApiKey.
    public static IServiceCollection AddDeviceTenantClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration[DeviceTenantClient.BaseUrlConfigurationKey] ?? "http://localhost:5296";
        var internalServiceKey = configuration[InternalServiceAuthentication.ConfigurationKey];

        services.AddMemoryCache();
        services.AddHttpClient<IDeviceTenantClient, DeviceTenantClient>(client =>
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
