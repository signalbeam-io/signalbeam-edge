using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for calling IdentityManager service to validate device quotas.
/// </summary>
public class IdentityManagerClient : IDeviceQuotaValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IdentityManagerClient> _logger;

    public IdentityManagerClient(HttpClient httpClient, ILogger<IdentityManagerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result> CheckDeviceQuotaAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking device quota for tenant {TenantId}", tenantId.Value);

            var request = new CheckDeviceQuotaRequest(tenantId.Value);
            var response = await _httpClient.PostAsJsonAsync("/api/subscriptions/check-device-quota", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Device quota check passed for tenant {TenantId}", tenantId.Value);
                return Result.Success();
            }

            // Handle error responses
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);

                _logger.LogWarning("Device quota exceeded for tenant {TenantId}: {Message}",
                    tenantId.Value, errorResponse?.Message);

                return Result.Failure(
                    Error.Validation(
                        errorResponse?.Error ?? "DEVICE_QUOTA_EXCEEDED",
                        errorResponse?.Message ?? "Device quota exceeded. Please upgrade your subscription."));
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);

                _logger.LogWarning("Tenant or subscription not found for {TenantId}", tenantId.Value);

                return Result.Failure(
                    Error.NotFound(
                        errorResponse?.Error ?? "TENANT_NOT_FOUND",
                        errorResponse?.Message ?? "Tenant or subscription not found."));
            }

            _logger.LogError("Unexpected response from IdentityManager: {StatusCode}", response.StatusCode);
            return Result.Failure(
                Error.Failure(
                    "QUOTA_CHECK_FAILED",
                    "Unable to verify device quota. Please try again."));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when checking device quota for tenant {TenantId}", tenantId.Value);
            return Result.Failure(
                Error.Failure(
                    "IDENTITY_MANAGER_UNAVAILABLE",
                    "Unable to connect to IdentityManager service. Please try again."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking device quota for tenant {TenantId}", tenantId.Value);
            return Result.Failure(
                Error.Failure(
                    "QUOTA_CHECK_ERROR",
                    "An unexpected error occurred while checking device quota."));
        }
    }

    private record CheckDeviceQuotaRequest(Guid TenantId);
    private record CheckDeviceQuotaResponse(bool CanAddDevice, string Message);
    private record ErrorResponse(string Error, string Message);
}
