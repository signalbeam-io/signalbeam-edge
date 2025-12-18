using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get all bundles for a tenant.
/// </summary>
public record GetBundlesQuery(Guid TenantId);

/// <summary>
/// Bundle summary DTO.
/// </summary>
public record BundleSummaryDto(
    Guid BundleId,
    string Name,
    string? Description,
    string? LatestVersion,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for GetBundlesQuery.
/// </summary>
public record GetBundlesResponse(
    IReadOnlyList<BundleSummaryDto> Bundles);

/// <summary>
/// Handler for GetBundlesQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetBundlesHandler
{
    private readonly IBundleRepository _bundleRepository;

    public GetBundlesHandler(IBundleRepository bundleRepository)
    {
        _bundleRepository = bundleRepository;
    }

    public async Task<Result<GetBundlesResponse>> Handle(
        GetBundlesQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(query.TenantId);

        // Get all bundles for the tenant
        var bundles = await _bundleRepository.GetAllAsync(tenantId, cancellationToken);

        // Map to DTOs
        var bundleDtos = bundles.Select(b => new BundleSummaryDto(
            b.Id.Value,
            b.Name,
            b.Description,
            b.LatestVersion?.ToString(),
            b.CreatedAt
        )).ToList();

        return Result<GetBundlesResponse>.Success(new GetBundlesResponse(bundleDtos));
    }
}
