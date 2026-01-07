using Microsoft.Extensions.Logging;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Queries;

/// <summary>
/// Query to get all tenants with their data retention policies.
/// Used by background workers for data retention enforcement.
/// </summary>
public record GetTenantsWithRetentionQuery;

/// <summary>
/// Tenant retention policy information.
/// </summary>
public record TenantRetentionDto(
    Guid TenantId,
    string TenantName,
    int DataRetentionDays);

/// <summary>
/// Handler for GetTenantsWithRetentionQuery.
/// </summary>
public class GetTenantsWithRetentionHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<GetTenantsWithRetentionHandler> _logger;

    public GetTenantsWithRetentionHandler(
        ITenantRepository tenantRepository,
        ILogger<GetTenantsWithRetentionHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyCollection<TenantRetentionDto>>> Handle(
        GetTenantsWithRetentionQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching all active tenants with retention policies");

            var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

            var retentionDtos = tenants
                .Select(t => new TenantRetentionDto(
                    t.Id.Value,
                    t.Name,
                    t.DataRetentionDays))
                .ToList();

            _logger.LogInformation("Found {Count} tenants with retention policies", retentionDtos.Count);

            return Result.Success<IReadOnlyCollection<TenantRetentionDto>>(retentionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenants with retention policies");
            return Result.Failure<IReadOnlyCollection<TenantRetentionDto>>(
                Error.Unexpected(
                    "TENANT_FETCH_FAILED",
                    $"Failed to fetch tenants: {ex.Message}"));
        }
    }
}
