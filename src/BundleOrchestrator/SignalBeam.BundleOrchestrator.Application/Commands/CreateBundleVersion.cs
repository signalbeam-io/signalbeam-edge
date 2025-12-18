using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Container specification for CreateBundleVersionCommand.
/// </summary>
public record ContainerSpecDto(
    string Name,
    string Image,
    Dictionary<string, string>? Environment = null,
    List<string>? Ports = null);

/// <summary>
/// Command to create a new version of an app bundle.
/// </summary>
public record CreateBundleVersionCommand(
    string BundleId,
    string Version,
    List<ContainerSpecDto> Containers,
    string? ReleaseNotes = null);

/// <summary>
/// Response after creating a bundle version.
/// </summary>
public record CreateBundleVersionResponse(
    Guid VersionId,
    Guid BundleId,
    string Version,
    int ContainerCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for CreateBundleVersionCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class CreateBundleVersionHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;

    public CreateBundleVersionHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
    }

    public async Task<Result<CreateBundleVersionResponse>> Handle(
        CreateBundleVersionCommand command,
        CancellationToken cancellationToken)
    {
        // Parse and validate bundle ID
        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<CreateBundleVersionResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {command.BundleId}"));
        }

        // Check if bundle exists
        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<CreateBundleVersionResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {command.BundleId} not found."));
        }

        // Parse and validate version
        if (!BundleVersion.TryParse(command.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<CreateBundleVersionResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Version}"));
        }

        // Check if version already exists
        var existingVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (existingVersion is not null)
        {
            return Result.Failure<CreateBundleVersionResponse>(
                Error.Conflict("VERSION_ALREADY_EXISTS", $"Version {command.Version} already exists for bundle {bundle.Name}."));
        }

        // Validate and create container specs
        if (command.Containers == null || command.Containers.Count == 0)
        {
            return Result.Failure<CreateBundleVersionResponse>(
                Error.Validation("NO_CONTAINERS", "Bundle version must have at least one container."));
        }

        var containerSpecs = new List<ContainerSpec>();
        foreach (var containerDto in command.Containers)
        {
            try
            {
                var environmentJson = containerDto.Environment != null
                    ? System.Text.Json.JsonSerializer.Serialize(containerDto.Environment)
                    : null;

                var portsJson = containerDto.Ports != null
                    ? System.Text.Json.JsonSerializer.Serialize(containerDto.Ports)
                    : null;

                var containerSpec = ContainerSpec.Create(
                    containerDto.Name,
                    containerDto.Image,
                    environmentJson,
                    portsJson);

                containerSpecs.Add(containerSpec);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure<CreateBundleVersionResponse>(
                    Error.Validation("INVALID_CONTAINER_SPEC", $"Invalid container specification: {ex.Message}"));
            }
        }

        // Create new bundle version
        var versionId = Guid.NewGuid();
        var appBundleVersion = AppBundleVersion.Create(
            versionId,
            bundleId,
            bundleVersion,
            containerSpecs,
            command.ReleaseNotes,
            DateTimeOffset.UtcNow);

        // Update bundle's latest version
        bundle.UpdateLatestVersion(bundleVersion);

        // Save to repositories
        await _bundleVersionRepository.AddAsync(appBundleVersion, cancellationToken);
        await _bundleRepository.UpdateAsync(bundle, cancellationToken);
        await _bundleVersionRepository.SaveChangesAsync(cancellationToken);
        await _bundleRepository.SaveChangesAsync(cancellationToken);

        // Return response
        return Result<CreateBundleVersionResponse>.Success(new CreateBundleVersionResponse(
            versionId,
            bundle.Id.Value,
            bundleVersion.ToString(),
            containerSpecs.Count,
            appBundleVersion.CreatedAt));
    }
}
