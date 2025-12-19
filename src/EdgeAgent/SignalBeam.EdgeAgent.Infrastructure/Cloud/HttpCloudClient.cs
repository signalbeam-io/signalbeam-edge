using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Cloud;

public class HttpCloudClient : ICloudClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpCloudClient> _logger;

    public HttpCloudClient(HttpClient httpClient, ILogger<HttpCloudClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registering device {DeviceId} with tenant {TenantId}", request.DeviceId, request.TenantId);

            var response = await _httpClient.PostAsJsonAsync("/api/devices/register", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize registration response");
            }

            _logger.LogInformation("Device registered successfully with ID: {DeviceId}", result.DeviceId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device");
            throw;
        }
    }

    public async Task SendHeartbeatAsync(
        DeviceHeartbeat heartbeat,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending heartbeat for device {DeviceId}", heartbeat.DeviceId);

            var response = await _httpClient.PostAsJsonAsync("/api/devices/heartbeat", heartbeat, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Heartbeat sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send heartbeat");
            throw;
        }
    }

    public async Task<DesiredState?> FetchDesiredStateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching desired state for device {DeviceId}", deviceId);

            var response = await _httpClient.GetAsync($"/api/devices/{deviceId}/desired-state", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No desired state found for device {DeviceId}", deviceId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DesiredState>(cancellationToken);

            _logger.LogDebug("Desired state fetched successfully");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch desired state");
            throw;
        }
    }

    public async Task ReportCurrentStateAsync(
        DeviceCurrentState currentState,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Reporting current state for device {DeviceId}", currentState.DeviceId);

            var response = await _httpClient.PostAsJsonAsync("/api/devices/current-state", currentState, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Current state reported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report current state");
            throw;
        }
    }
}
