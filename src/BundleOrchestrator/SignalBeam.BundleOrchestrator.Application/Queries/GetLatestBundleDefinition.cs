using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Storage;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using System.Text.Json;

namespace SignalBeam.BundleOrchestrator.Application.Queries;

/// <summary>
/// Query to get the latest bundle definition for a bundle.
/// </summary>
public record GetLatestBundleDefinitionQuery(string BundleId);

/// <summary>
/// Response for GetLatestBundleDefinitionQuery.
/// </summary>
public record GetLatestBundleDefinitionResponse(
    BundleDefinition Definition,
    string Checksum,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for GetLatestBundleDefinitionQuery.
/// </summary>
public class GetLatestBundleDefinitionHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IBundleStorageService _bundleStorageService;

    public GetLatestBundleDefinitionHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IBundleStorageService bundleStorageService)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _bundleStorageService = bundleStorageService;
    }

    public async Task<Result<GetLatestBundleDefinitionResponse>> Handle(
        GetLatestBundleDefinitionQuery query,
        CancellationToken cancellationToken)
    {
        if (!BundleId.TryParse(query.BundleId, out var bundleId))
        {
            return Result.Failure<GetLatestBundleDefinitionResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {query.BundleId}"));
        }

        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<GetLatestBundleDefinitionResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {query.BundleId} not found."));
        }

        if (bundle.LatestVersion is null)
        {
            return Result.Failure<GetLatestBundleDefinitionResponse>(
                Error.NotFound("LATEST_VERSION_NOT_FOUND", $"Bundle {query.BundleId} has no published versions."));
        }

        var latestVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundle.LatestVersion,
            cancellationToken);

        if (latestVersion is null)
        {
            return Result.Failure<GetLatestBundleDefinitionResponse>(
                Error.NotFound(
                    "VERSION_NOT_FOUND",
                    $"Latest version {bundle.LatestVersion} not found for bundle {query.BundleId}."));
        }

        try
        {
            await using var content = await _bundleStorageService.DownloadBundleManifestAsync(
                bundle.TenantId.Value.ToString(),
                bundleId.Value.ToString(),
                bundle.LatestVersion.ToString(),
                cancellationToken);

            var definition = await JsonSerializer.DeserializeAsync<BundleDefinition>(content, cancellationToken: cancellationToken);
            if (definition is null)
            {
                return Result.Failure<GetLatestBundleDefinitionResponse>(
                    Error.NotFound("BUNDLE_DEFINITION_MISSING", "Bundle definition could not be read."));
            }

            definition = new BundleDefinition
            {
                BundleId = definition.BundleId,
                Version = definition.Version,
                Description = definition.Description,
                Checksum = latestVersion.Checksum ?? definition.Checksum,
                Containers = definition.Containers
            };

            return Result<GetLatestBundleDefinitionResponse>.Success(
                new GetLatestBundleDefinitionResponse(
                    definition,
                    latestVersion.Checksum ?? definition.Checksum,
                    latestVersion.CreatedAt));
        }
        catch (Exception ex)
        {
            return Result.Failure<GetLatestBundleDefinitionResponse>(
                Error.Unexpected("BUNDLE_DEFINITION_ERROR", $"Failed to load bundle definition: {ex.Message}"));
        }
    }
}
