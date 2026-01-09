using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

/// <summary>
/// HTTP client for retrieving device information from DeviceManager.
/// </summary>
public class DeviceClient : IDeviceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeviceClient> _logger;

    public DeviceClient(HttpClient httpClient, ILogger<DeviceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all device IDs for a specific tenant from DeviceManager.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<DeviceId>>> GetDeviceIdsByTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching device IDs for tenant {TenantId}", tenantId.Value);

            var response = await _httpClient.GetAsync(
                $"/api/devices?tenantId={tenantId.Value}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to fetch devices for tenant {TenantId}. Status: {StatusCode}",
                    tenantId.Value,
                    response.StatusCode);

                return Result.Failure<IReadOnlyCollection<DeviceId>>(
                    Error.Failure(
                        "DEVICE_MANAGER_UNAVAILABLE",
                        $"Failed to fetch devices. Status: {response.StatusCode}"));
            }

            var devices = await response.Content.ReadFromJsonAsync<List<DeviceResponse>>(
                cancellationToken: cancellationToken);

            if (devices == null)
            {
                _logger.LogError("Received null response from DeviceManager for tenant {TenantId}", tenantId.Value);
                return Result.Failure<IReadOnlyCollection<DeviceId>>(
                    Error.Failure(
                        "INVALID_RESPONSE",
                        "Received invalid response from DeviceManager"));
            }

            var deviceIds = devices
                .Select(d => new DeviceId(d.Id))
                .ToList();

            _logger.LogDebug(
                "Successfully fetched {Count} device IDs for tenant {TenantId}",
                deviceIds.Count,
                tenantId.Value);

            return Result.Success<IReadOnlyCollection<DeviceId>>(deviceIds);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching devices for tenant {TenantId}", tenantId.Value);
            return Result.Failure<IReadOnlyCollection<DeviceId>>(
                Error.Failure(
                    "HTTP_REQUEST_FAILED",
                    $"Failed to connect to DeviceManager: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching devices for tenant {TenantId}", tenantId.Value);
            return Result.Failure<IReadOnlyCollection<DeviceId>>(
                Error.Unexpected(
                    "UNEXPECTED_ERROR",
                    $"Unexpected error: {ex.Message}"));
        }
    }

    private record DeviceResponse(Guid Id);
}
