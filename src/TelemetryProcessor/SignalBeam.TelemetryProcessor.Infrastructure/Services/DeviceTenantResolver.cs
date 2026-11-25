using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.TelemetryProcessor.Application.Services;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

// Looks up a device's tenant from DeviceManager's internal endpoint. A device never changes
// tenant, so hits are cached for long; misses briefly, so a newly registered device shows up soon.
public sealed class DeviceTenantResolver : IDeviceTenantResolver
{
    internal static readonly TimeSpan HitTtl = TimeSpan.FromHours(6);
    internal static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DeviceTenantResolver> _logger;

    public DeviceTenantResolver(HttpClient httpClient, IMemoryCache cache, ILogger<DeviceTenantResolver> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TenantId?> ResolveAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"device-tenant:{deviceId.Value}";
        if (_cache.TryGetValue(cacheKey, out TenantId? cached))
        {
            return cached;
        }

        TenantId? tenantId = null;
        try
        {
            using var response = await _httpClient.GetAsync(
                $"/api/internal/devices/{deviceId.Value}/tenant", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<DeviceTenantResponse>(cancellationToken);
                if (body is not null && body.TenantId != Guid.Empty)
                {
                    tenantId = new TenantId(body.TenantId);
                }
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
            {
                // 401: InternalServices:ApiKey missing or different from DeviceManager's.
                _logger.LogWarning("Device tenant lookup for {DeviceId} failed: {StatusCode}", deviceId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Device tenant lookup for {DeviceId} failed", deviceId);
            return null;
        }

        _cache.Set(cacheKey, tenantId, tenantId is null ? MissTtl : HitTtl);
        return tenantId;
    }

    private sealed record DeviceTenantResponse(Guid DeviceId, Guid TenantId);
}
