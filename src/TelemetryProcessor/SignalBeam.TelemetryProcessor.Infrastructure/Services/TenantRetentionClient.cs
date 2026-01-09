using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

/// <summary>
/// HTTP client for retrieving tenant retention policies from IdentityManager.
/// </summary>
public class TenantRetentionClient : ITenantRetentionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantRetentionClient> _logger;

    public TenantRetentionClient(HttpClient httpClient, ILogger<TenantRetentionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all tenants with their data retention policies from IdentityManager.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<TenantRetentionInfo>>> GetAllTenantsWithRetentionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching tenant retention policies from IdentityManager");

            var response = await _httpClient.GetAsync("/api/tenants/retention-policies", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to fetch tenant retention policies. Status: {StatusCode}",
                    response.StatusCode);

                return Result.Failure<IReadOnlyCollection<TenantRetentionInfo>>(
                    Error.Failure(
                        "IDENTITY_MANAGER_UNAVAILABLE",
                        $"Failed to fetch tenant retention policies. Status: {response.StatusCode}"));
            }

            var tenants = await response.Content.ReadFromJsonAsync<List<TenantRetentionResponse>>(
                cancellationToken: cancellationToken);

            if (tenants == null)
            {
                _logger.LogError("Received null response from IdentityManager");
                return Result.Failure<IReadOnlyCollection<TenantRetentionInfo>>(
                    Error.Failure(
                        "INVALID_RESPONSE",
                        "Received invalid response from IdentityManager"));
            }

            var retentionInfos = tenants
                .Select(t => new TenantRetentionInfo(t.TenantId, t.TenantName, t.DataRetentionDays))
                .ToList();

            _logger.LogInformation("Successfully fetched {Count} tenant retention policies", retentionInfos.Count);

            return Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(retentionInfos);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching tenant retention policies");
            return Result.Failure<IReadOnlyCollection<TenantRetentionInfo>>(
                Error.Failure(
                    "HTTP_REQUEST_FAILED",
                    $"Failed to connect to IdentityManager: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching tenant retention policies");
            return Result.Failure<IReadOnlyCollection<TenantRetentionInfo>>(
                Error.Unexpected(
                    "UNEXPECTED_ERROR",
                    $"Unexpected error: {ex.Message}"));
        }
    }

    private record TenantRetentionResponse(
        Guid TenantId,
        string TenantName,
        int DataRetentionDays);
}
