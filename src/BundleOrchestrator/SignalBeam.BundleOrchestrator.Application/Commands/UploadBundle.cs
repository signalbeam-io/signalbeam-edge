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
/// Command to upload a new bundle definition and create the bundle with its initial version.
/// </summary>
public record UploadBundleCommand(
    Guid TenantId,
    BundleDefinition Definition);

/// <summary>
/// Response after uploading a bundle definition.
/// </summary>
public record UploadBundleResponse(
    Guid BundleId,
    string Version,
    string BlobStorageUri,
    string Checksum,
    long SizeBytes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handler for UploadBundleCommand.
/// </summary>
public class UploadBundleHandler
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IBundleStorageService _bundleStorageService;
    private readonly IValidator<BundleDefinition> _bundleDefinitionValidator;

    public UploadBundleHandler(
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

    public async Task<Result<UploadBundleResponse>> Handle(
        UploadBundleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty)
        {
            return Result.Failure<UploadBundleResponse>(
                Error.Validation("INVALID_TENANT_ID", "Tenant ID is required."));
        }

        if (command.Definition is null)
        {
            return Result.Failure<UploadBundleResponse>(
                Error.Validation("INVALID_BUNDLE_DEFINITION", "Bundle definition is required."));
        }

        var validation = await _bundleDefinitionValidator.ValidateAsync(command.Definition, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<UploadBundleResponse>(
                Error.Validation("INVALID_BUNDLE_DEFINITION", message));
        }

        if (!BundleId.TryParse(command.Definition.BundleId, out var bundleId))
        {
            return Result.Failure<UploadBundleResponse>(
                Error.Validation("INVALID_BUNDLE_ID", $"Invalid bundle ID format: {command.Definition.BundleId}"));
        }

        if (!BundleVersion.TryParse(command.Definition.Version, out var bundleVersion) || bundleVersion is null)
        {
            return Result.Failure<UploadBundleResponse>(
                Error.Validation("INVALID_VERSION", $"Invalid semantic version format: {command.Definition.Version}"));
        }

        var tenantId = new TenantId(command.TenantId);

        var existingBundle = await _bundleRepository.GetByIdAsync(bundleId, cancellationToken);
        if (existingBundle is not null)
        {
            return Result.Failure<UploadBundleResponse>(
                Error.Conflict("BUNDLE_ALREADY_EXISTS", $"Bundle with ID {command.Definition.BundleId} already exists."));
        }

        var bundle = AppBundle.Create(
            bundleId,
            tenantId,
            command.Definition.BundleId,
            command.Definition.Description,
            DateTimeOffset.UtcNow);

        var containerSpecs = CreateContainerSpecs(command.Definition, out var containerError);
        if (containerError is not null)
        {
            return Result.Failure<UploadBundleResponse>(containerError);
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
            return Result.Failure<UploadBundleResponse>(
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
            tenantId.Value.ToString(),
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

        await _bundleRepository.AddAsync(bundle, cancellationToken);
        await _bundleVersionRepository.AddAsync(appBundleVersion, cancellationToken);
        await _bundleRepository.SaveChangesAsync(cancellationToken);
        await _bundleVersionRepository.SaveChangesAsync(cancellationToken);

        return Result<UploadBundleResponse>.Success(new UploadBundleResponse(
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
