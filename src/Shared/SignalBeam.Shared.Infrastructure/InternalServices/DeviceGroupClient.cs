using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.InternalServices;

public enum DeviceGroupLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

public sealed record DeviceGroupMembers(Guid GroupId, string Name, IReadOnlyList<Guid> DeviceIds);

public readonly record struct DeviceGroupLookup(DeviceGroupLookupStatus Status, DeviceGroupMembers? Group)
{
    public static DeviceGroupLookup Found(DeviceGroupMembers group) => new(DeviceGroupLookupStatus.Found, group);
    public static readonly DeviceGroupLookup NotFound = new(DeviceGroupLookupStatus.NotFound, null);
    public static readonly DeviceGroupLookup Unavailable = new(DeviceGroupLookupStatus.Unavailable, null);
}

public interface IDeviceGroupClient
{
    // NotFound covers both a missing group and another tenant's group.
    Task<DeviceGroupLookup> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken = default);
}

// Reads a device group and its members from DeviceManager's internal endpoint. Not cached:
// membership changes often and rollout commands are rare.
public sealed class DeviceGroupClient : IDeviceGroupClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeviceGroupClient> _logger;

    public DeviceGroupClient(HttpClient httpClient, ILogger<DeviceGroupClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DeviceGroupLookup> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{InternalServiceAuthentication.PathPrefix}/tenants/{tenantId}/groups/{groupId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return DeviceGroupLookup.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                // 401: InternalServices:ApiKey missing or different from DeviceManager's.
                _logger.LogWarning("Device group lookup for {GroupId} failed: {StatusCode}", groupId, response.StatusCode);
                return DeviceGroupLookup.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<DeviceGroupMembers>(cancellationToken);
            return body is { DeviceIds: not null } && body.GroupId == groupId
                ? DeviceGroupLookup.Found(body)
                : DeviceGroupLookup.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Device group lookup for {GroupId} failed", groupId);
            return DeviceGroupLookup.Unavailable;
        }
    }
}

public static class DeviceGroupClientExtensions
{
    // Registers IDeviceGroupClient against DeviceManager:BaseUrl, sending InternalServices:ApiKey.
    public static IServiceCollection AddDeviceGroupClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration[DeviceTenantClient.BaseUrlConfigurationKey] ?? "http://localhost:5296";
        var internalServiceKey = configuration[InternalServiceAuthentication.ConfigurationKey];

        services.AddHttpClient<IDeviceGroupClient, DeviceGroupClient>(client =>
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
