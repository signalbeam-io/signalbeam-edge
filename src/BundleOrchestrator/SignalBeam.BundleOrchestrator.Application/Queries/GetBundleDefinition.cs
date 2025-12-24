using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Storage;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using System.Text.Json;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get a bundle definition for a specific version.
/// </summary>
public record GetBundleDefinitionQuery(
    string BundleId,
    string Version);

/// <summary>
/// Response for GetBundleDefinitionQuery.
/// </summary>
public record GetBundleDefinitionResponse(
    BundleDefinition Definition,
    string Checksum,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for GetBundleDefinitionQuery.
/// </summary>
public class GetBundleDefinitionHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IBundleStorageService _bundleStorageService;

    public GetBundleDefinitionHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IBundleStorageService bundleStorageService)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _bundleStorageService = bundleStorageService;
    }

    public async Task<Result<GetBundleDefinitionResponse>> Handle(
        GetBundleDefinitionQuery query,
        CancellationToken cancellationToken)
    {
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetBundleDefinitionResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        if (!BundleVersion.TryParse(query.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<GetBundleDefinitionResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {query.Version}"));
        }

        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<GetBundleDefinitionResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {query.BundleId} not found."));
        }

        var version = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (version is null)
        {
            return Result.Failure<GetBundleDefinitionResponse>(
                Error.NotFound("VERSION_NOT_FOUND", $"Version {query.Version} not found for bundle {query.BundleId}."));
        }

        try
        {
            await using var content = await _bundleStorageService.DownloadBundleManifestAsync(
                bundle.TenantId.Value.ToString(),
                bundleId.Value.ToString(),
                bundleVersion.ToString(),
                cancellationToken);

            var definition = await JsonSerializer.DeserializeAsync<BundleDefinition>(content, cancellationToken: cancellationToken);
            if (definition is null)
            {
                return Result.Failure<GetBundleDefinitionResponse>(
                    Error.NotFound("BUNDLE_DEFINITION_MISSING", "Bundle definition could not be read."));
            }

            definition = new BundleDefinition
            {
                BundleId = definition.BundleId,
                Version = definition.Version,
                Description = definition.Description,
                Checksum = version.Checksum ?? definition.Checksum,
                Containers = definition.Containers
            };

            return Result<GetBundleDefinitionResponse>.Success(
                new GetBundleDefinitionResponse(
                    definition,
                    version.Checksum ?? definition.Checksum,
                    version.CreatedAt));
        }
        catch (Exception ex)
        {
            return Result.Failure<GetBundleDefinitionResponse>(
                Error.Unexpected("BUNDLE_DEFINITION_ERROR", $"Failed to load bundle definition: {ex.Message}"));
        }
    }
}
