using FluentValidation;
using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Storage;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

/// <summary>
/// Command to upload a new version for an existing bundle.
/// </summary>
public record UploadBundleVersionCommand(
    string BundleId,
    BundleDefinition Definition);

/// <summary>
/// Response after uploading a bundle version.
/// </summary>
public record UploadBundleVersionResponse(
    Guid VersionId,
    Guid BundleId,
    string Version,
    string BlobStorageUri,
    string Checksum,
    long SizeBytes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for UploadBundleVersionCommand.
/// </summary>
public class UploadBundleVersionHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IBundleStorageService _bundleStorageService;
    private readonly IValidator<BundleDefinition> _bundleDefinitionValidator;

    public UploadBundleVersionHandler(
        IBundleRepository bundleRepository,
        IBundleVersionRepository bundleVersionRepository,
        IBundleStorageService bundleStorageService,
        IValidator<BundleDefinition> bundleDefinitionValidator)
    {
        _bundleRepository = bundleRepository;
        _bundleVersionRepository = bundleVersionRepository;
        _bundleStorageService = bundleStorageService;
        _bundleDefinitionValidator = bundleDefinitionValidator;
    }

    public async Task<Result<UploadBundleVersionResponse>> Handle(
        UploadBundleVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Definition is null)
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation("INVALID_BUNDLE_DEFINITION", "Bundle definition is required."));
        }

        var validation = await _bundleDefinitionValidator.ValidateAsync(command.Definition, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation("INVALID_BUNDLE_DEFINITION", message));
        }

        if (!BundleId.TryParse(command.BundleId, out var bundleId))
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {command.BundleId}"));
        }

        if (!BundleId.TryParse(command.Definition.BundleId, out var definitionBundleId) ||
            definitionBundleId != bundleId)
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation(
                    "BUNDLE_ID_MISMATCH",
                    $"Bundle definition bundleId '{command.Definition.BundleId}' does not match route bundleId '{command.BundleId}'."));
        }

        if (!BundleVersion.TryParse(command.Definition.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Definition.Version}"));
        }

        var bundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (bundle is null)
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.NotFound("BUNDLE_NOT_FOUND", $"Bundle with ID {command.BundleId} not found."));
        }

        var existingVersion = await _bundleVersionRepository.GetByBundleAndVersionAsync(
            bundleId,
            bundleVersion,
            cancellationToken);

        if (existingVersion is not null)
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Conflict(
                    "VERSION_ALREADY_EXISTS",
                    $"Version {command.Definition.Version} already exists for bundle {command.BundleId}."));
        }

        var containerSpecs = CreateContainerSpecs(command.Definition, out var containerError);
        if (containerError is not null)
        {
            return Result.Failure<UploadBundleVersionResponse>(containerError);
        }

        var versionId = Guid.NewGuid();
        var appBundleVersion = AppBundleVersion.Create(
            versionId,
            bundleId,
            bundleVersion,
            containerSpecs,
            null,
            DateTimeOffset.UtcNow);

        var definitionPayload = BuildDefinitionPayload(command.Definition);
        var checksum = CalculateChecksum(definitionPayload);

        if (!string.IsNullOrWhiteSpace(command.Definition.Checksum) &&
            !string.Equals(command.Definition.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<UploadBundleVersionResponse>(
                Error.Validation(
                    "CHECKSUM_MISMATCH",
                    "Provided checksum does not match calculated checksum."));
        }

        var definitionForStorage = new BundleDefinition
        {
            BundleId = command.Definition.BundleId,
            Version = command.Definition.Version,
            Description = command.Definition.Description,
            Checksum = checksum,
            Containers = command.Definition.Containers
        };

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(definitionForStorage, GetSerializerOptions());
        using var payloadStream = new MemoryStream(payloadBytes);

        var metadata = await _bundleStorageService.UploadBundleWithMetadataAsync(
            bundle.TenantId.Value.ToString(),
            bundleId.Value.ToString(),
            bundleVersion.ToString(),
            payloadStream,
            checksum,
            cancellationToken);

        appBundleVersion.SetBlobStorageMetadata(
            metadata.BlobStorageUri,
            metadata.Checksum,
            metadata.SizeBytes);

        appBundleVersion.Publish();

        bundle.UpdateLatestVersion(bundleVersion);

        await _bundleVersionRepository.AddAsync(appBundleVersion, cancellationToken);
        await _bundleRepository.UpdateAsync(bundle, cancellationToken);
        await _bundleVersionRepository.SaveChangesAsync(cancellationToken);
        await _bundleRepository.SaveChangesAsync(cancellationToken);

        return Result<UploadBundleVersionResponse>.Success(new UploadBundleVersionResponse(
            versionId,
            bundleId.Value,
            bundleVersion.ToString(),
            metadata.BlobStorageUri,
            metadata.Checksum,
            metadata.SizeBytes,
            appBundleVersion.CreatedAt));
    }

    private static List<ContainerSpec> CreateContainerSpecs(
        BundleDefinition definition,
        out Error? error)
    {
        var containerSpecs = new List<ContainerSpec>();
        error = null;

        var options = GetSerializerOptions();

        foreach (var container in definition.Containers)
        {
            try
            {
                var environmentJson = container.Env is not null
                    ? JsonSerializer.Serialize(container.Env, options)
                    : null;

                var portsJson = container.Ports is not null
                    ? JsonSerializer.Serialize(container.Ports, options)
                    : null;

                var volumesJson = container.Volumes is not null
                    ? JsonSerializer.Serialize(container.Volumes, options)
                    : null;

                var additionalParameters = BuildAdditionalParameters(container, options);

                var containerSpec = ContainerSpec.Create(
                    container.Name,
                    container.Image,
                    environmentJson,
                    portsJson,
                    volumesJson,
                    additionalParameters);

                containerSpecs.Add(containerSpec);
            }
            catch (ArgumentException ex)
            {
                error = Error.Validation("INVALID_CONTAINER_SPEC", $"Invalid container specification: {ex.Message}");
                return containerSpecs;
            }
        }

        return containerSpecs;
    }

    private static string BuildDefinitionPayload(BundleDefinition definition)
    {
        var definitionPayload = new BundleDefinition
        {
            BundleId = definition.BundleId,
            Version = definition.Version,
            Description = definition.Description,
            Checksum = string.Empty,
            Containers = definition.Containers
        };

        return JsonSerializer.Serialize(definitionPayload, GetSerializerOptions());
    }

    private static string CalculateChecksum(string payload)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = sha256.ComputeHash(bytes);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string? BuildAdditionalParameters(
        ContainerDefinition container,
        JsonSerializerOptions options)
    {
        var additional = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(container.ImagePullPolicy))
        {
            additional["imagePullPolicy"] = container.ImagePullPolicy;
        }

        if (!string.IsNullOrWhiteSpace(container.RestartPolicy))
        {
            additional["restartPolicy"] = container.RestartPolicy;
        }

        if (container.Resources is not null)
        {
            additional["resources"] = container.Resources;
        }

        return additional.Count > 0
            ? JsonSerializer.Serialize(additional, options)
            : null;
    }

    private static JsonSerializerOptions GetSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
