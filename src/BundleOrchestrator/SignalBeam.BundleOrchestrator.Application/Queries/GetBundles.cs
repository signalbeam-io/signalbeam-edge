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
    DateTimeOffset CreatedAt,
    IReadOnlyList<BundleVersionSummaryDto> Versions);

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
    private readonly IBundleVersionRepository _bundleVersionRepository;

    public GetBundlesHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
    }

    public async Task<Result<GetBundlesResponse>> Handle(
        GetBundlesQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(query.TenantId);

        // Get all bundles for the tenant
        var bundles = await _bundleRepository.GetAllAsync(tenantId, cancellationToken);

        // Map to DTOs
        var bundleDtos = new List<BundleSummaryDto>();
        foreach (var bundle in bundles)
        {
            var versions = await _bundleVersionRepository.GetAllVersionsAsync(bundle.Id, cancellationToken);
            var versionDtos = versions.Select(v => new BundleVersionSummaryDto(
                v.Id,
                v.Version.ToString(),
                v.Containers.Select(c => new ContainerSpecDetailDto(
                    c.Name,
                    c.Image,
                    c.EnvironmentVariables,
                    c.PortMappings,
                    c.VolumeMounts,
                    c.AdditionalParameters)).ToList(),
                v.Containers.Count,
                v.ReleaseNotes,
                v.CreatedAt
            )).ToList();

            bundleDtos.Add(new BundleSummaryDto(
                bundle.Id.Value,
                bundle.Name,
                bundle.Description,
                bundle.LatestVersion?.ToString(),
                bundle.CreatedAt,
                versionDtos));
        }

        return Result<GetBundlesResponse>.Success(new GetBundlesResponse(bundleDtos));
    }
}
