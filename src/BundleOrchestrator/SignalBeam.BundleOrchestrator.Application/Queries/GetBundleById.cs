using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get a bundle by ID with all its versions.
/// </summary>
public record GetBundleByIdQuery(string BundleId);

/// <summary>
/// Bundle version summary DTO.
/// </summary>
public record BundleVersionSummaryDto(
    Guid VersionId,
    string Version,
    IReadOnlyList<ContainerSpecDetailDto> Containers,
    int ContainerCount,
    string? ReleaseNotes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Bundle detail DTO.
/// </summary>
public record BundleDetailDto(
    Guid BundleId,
    string Name,
    string? Description,
    string? LatestVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BundleVersionSummaryDto> Versions);

/// <summary>
/// Response for GetBundleByIdQuery.
/// </summary>
public record GetBundleByIdResponse(BundleDetailDto Bundle);

/// <summary>
/// Handler for GetBundleByIdQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetBundleByIdHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;

    public GetBundleByIdHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
    }

    public async Task<Result<GetBundleByIdResponse>> Handle(
        GetBundleByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Parse and validate bundle ID
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetBundleByIdResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        // Get bundle
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<GetBundleByIdResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {query.BundleId} not found."));
        }

        // Get all versions of the bundle
        var versions = await _bundleVersionRepository.GetAllVersionsAsync(bundleId, cancellationToken);

        // Map to DTOs
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

        var bundleDto = new BundleDetailDto(
            bundle.Id.Value,
            bundle.Name,
            bundle.Description,
            bundle.LatestVersion?.ToString(),
            bundle.CreatedAt,
            versionDtos);

        return Result<GetBundleByIdResponse>.Success(new GetBundleByIdResponse(bundleDto));
    }
}
