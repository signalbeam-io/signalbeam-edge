using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get a specific version of a bundle.
/// </summary>
public record GetBundleVersionQuery(
    string BundleId,
    string Version);

/// <summary>
/// Container specification DTO.
/// </summary>
public record ContainerSpecDetailDto(
    string Name,
    string Image,
    string? EnvironmentVariables,
    string? PortMappings,
    string? VolumeMounts,
    string? AdditionalParameters);

/// <summary>
/// Bundle version detail DTO.
/// </summary>
public record BundleVersionDetailDto(
    Guid VersionId,
    Guid BundleId,
    string Version,
    IReadOnlyList<ContainerSpecDetailDto> Containers,
    string? ReleaseNotes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for GetBundleVersionQuery.
/// </summary>
public record GetBundleVersionResponse(BundleVersionDetailDto BundleVersion);

/// <summary>
/// Handler for GetBundleVersionQuery.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class GetBundleVersionHandler
{
    private readonly IBundleVersionRepository _bundleVersionRepository;

    public GetBundleVersionHandler(IBundleVersionRepository bundleVersionRepository)
    {
        _bundleVersionRepository = bundleVersionRepository;
    }

    public async Task<Result<GetBundleVersionResponse>> Handle(
        GetBundleVersionQuery query,
        CancellationToken cancellationToken)
    {
        // Parse and validate bundle ID
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetBundleVersionResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        // Parse and validate version
        if (!BundleVersion.TryParse(query.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<GetBundleVersionResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {query.Version}"));
        }

        // Get bundle version
        var appBundleVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (appBundleVersion is null)
        {
            return Result.Failure<GetBundleVersionResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {query.Version} not found for bundle {query.BundleId}."));
        }

        // Map to DTOs
        var containerDtos = appBundleVersion.Containers.Select(c => new ContainerSpecDetailDto(
            c.Name,
            c.Image,
            c.EnvironmentVariables,
            c.PortMappings,
            c.VolumeMounts,
            c.AdditionalParameters
        )).ToList();

        var versionDto = new BundleVersionDetailDto(
            appBundleVersion.Id,
            appBundleVersion.BundleId.Value,
            appBundleVersion.Version.ToString(),
            containerDtos,
            appBundleVersion.ReleaseNotes,
            appBundleVersion.CreatedAt);

        return Result<GetBundleVersionResponse>.Success(new GetBundleVersionResponse(versionDto));
    }
}
