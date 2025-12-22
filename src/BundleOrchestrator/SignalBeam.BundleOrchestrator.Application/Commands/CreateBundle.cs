using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to create a new app bundle, optionally with an initial version.
/// </summary>
public record CreateBundleCommand(
    Guid TenantId,
    string Name,
    string? Description = null,
    string? Version = null,
    List<ContainerSpecDto>? Containers = null);

/// <summary>
/// Response after creating a bundle.
/// </summary>
public record CreateBundleResponse(
    Guid BundleId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    string? Version = null,
    Guid? VersionId = null);

/// <summary>
/// Handler for CreateBundleCommand.
/// Uses Wolverine's IMessageHandler pattern.
/// </summary>
public class CreateBundleHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;

    public CreateBundleHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
    }

    public async Task<Result<CreateBundleResponse>> Handle(
        CreateBundleCommand command,
        CancellationToken cancellationToken)
    {
        // Validate version and containers are provided together
        var hasVersion = !string.IsNullOrWhiteSpace(command.Version);
        var hasContainers = command.Containers?.Count > 0;

        if (hasVersion != hasContainers)
        {
            return Result.Failure<CreateBundleResponse>(
                Error.Validation(
                    "INVALID_VERSION_DATA",
                    "Version and containers must both be provided or both be omitted."));
        }

        // Validate tenant ID
        var tenantId = new TenantId(command.TenantId);

        // Check if bundle with same name already exists for this tenant
        var existingBundle = await _bundleRepository.GetByNameAsync(tenantId, command.Name, cancellationToken);
        if (existingBundle is not null)
        {
            var error = Error.Conflict(
                "BUNDLE_ALREADY_EXISTS",
                $"Bundle with name '{command.Name}' already exists for tenant {command.TenantId}.");
            return Result.Failure<CreateBundleResponse>(error);
        }

        // Create new bundle using factory method
        var bundleId = new BundleId(Guid.NewGuid());
        var bundle = AppBundle.Create(
            bundleId,
            tenantId,
            command.Name,
            command.Description,
            DateTimeOffset.UtcNow);

        // Save bundle
        await _bundleRepository.AddAsync(bundle, cancellationToken);
        await _bundleRepository.SaveChangesAsync(cancellationToken);

        // Create initial version if provided
        Guid? versionId = null;
        string? versionString = null;

        if (hasVersion && hasContainers)
        {
            // Parse and validate version
            if (!BundleVersion.TryParse(command.Version!, out var bundleVersion) || bundleVersion is null)
            {
                return Result.Failure<CreateBundleResponse>(
                    Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Version}"));
            }

            // Create container specs
            var containerSpecs = new List<ContainerSpec>();
            foreach (var containerDto in command.Containers!)
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
                    return Result.Failure<CreateBundleResponse>(
                        Error.Validation("INVALID_CONTAINER_SPEC", $"Invalid container specification: {ex.Message}"));
                }
            }

            // Create bundle version
            versionId = Guid.NewGuid();
            var appBundleVersion = AppBundleVersion.Create(
                versionId.Value,
                bundleId,
                bundleVersion,
                containerSpecs,
                null, // No release notes for initial version
                DateTimeOffset.UtcNow);

            // Update bundle's latest version
            bundle.UpdateLatestVersion(bundleVersion);

            // Save version and update bundle
            await _bundleVersionRepository.AddAsync(appBundleVersion, cancellationToken);
            await _bundleRepository.UpdateAsync(bundle, cancellationToken);
            await _bundleVersionRepository.SaveChangesAsync(cancellationToken);
            await _bundleRepository.SaveChangesAsync(cancellationToken);

            versionString = bundleVersion.ToString();
        }

        // Return response
        return Result<CreateBundleResponse>.Success(new CreateBundleResponse(
            bundle.Id.Value,
            bundle.Name,
            bundle.Description,
            bundle.CreatedAt,
            versionString,
            versionId));
    }
}
