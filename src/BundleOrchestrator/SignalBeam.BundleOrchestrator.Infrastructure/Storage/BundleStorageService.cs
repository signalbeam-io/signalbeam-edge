using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;

namespace SignalBeam.BundleOrchestrator.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation for bundle artifact storage.
/// Handles bundle manifest uploads and SAS token generation for edge agent downloads.
/// </summary>
public class BundleStorageService : IBundleStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BundleStorageService> _logger;
    private readonly string _containerName;

    public BundleStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<BundleStorageService> logger,
        string containerName = "bundle-manifests")
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _containerName = containerName;
    }

    public async Task<string> UploadBundleManifestAsync(
        string bundleId,
        string version,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("Bundle ID cannot be empty", nameof(bundleId));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty", nameof(version));

        if (content == null)
            throw new ArgumentNullException(nameof(content));

        try
        {
            var blobName = GetBlobName(bundleId, version);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                },
                Metadata = new Dictionary<string, string>
                {
                    { "bundleId", bundleId },
                    { "version", version },
                    { "uploadedAt", DateTimeOffset.UtcNow.ToString("O") }
                }
            };

            await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

            _logger.LogInformation(
                "Uploaded bundle manifest {BundleId} version {Version} to blob storage",
                bundleId,
                version);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to upload bundle manifest {BundleId} version {Version}",
                bundleId,
                version);
            throw;
        }
    }

    public async Task<string> GenerateBundleDownloadUrlAsync(
        string bundleId,
        string version,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("Bundle ID cannot be empty", nameof(bundleId));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty", nameof(version));

        try
        {
            var blobName = GetBlobName(bundleId, version);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if blob exists
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Bundle manifest {bundleId} version {version} does not exist in blob storage");
            }

            // Generate SAS token
            // Note: This requires the BlobServiceClient to be created with StorageSharedKeyCredential
            // or with Managed Identity that has permissions to generate SAS tokens
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = blobName,
                Resource = "b", // blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Start 5 minutes ago to account for clock skew
                ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            Uri sasUri;
            try
            {
                // Try to generate SAS token using delegated user credentials (Managed Identity)
                sasUri = blobClient.GenerateSasUri(sasBuilder);
            }
            catch (RequestFailedException)
            {
                // Fallback: If managed identity doesn't have permissions, return unsigned URL
                // Edge agents will need to use their own authentication
                _logger.LogWarning(
                    "Failed to generate SAS token for {BundleId} version {Version}. " +
                    "Returning unsigned URL. Ensure Managed Identity has 'Storage Blob Data Reader' role.",
                    bundleId,
                    version);

                sasUri = blobClient.Uri;
            }

            _logger.LogDebug(
                "Generated download URL for bundle {BundleId} version {Version} valid for {Validity}",
                bundleId,
                version,
                validity);

            return sasUri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate download URL for bundle {BundleId} version {Version}",
                bundleId,
                version);
            throw;
        }
    }

    public async Task<Stream> DownloadBundleManifestAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("Bundle ID cannot be empty", nameof(bundleId));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty", nameof(version));

        try
        {
            var blobName = GetBlobName(bundleId, version);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Downloaded bundle manifest {BundleId} version {Version}",
                bundleId,
                version);

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to download bundle manifest {BundleId} version {Version}",
                bundleId,
                version);
            throw;
        }
    }

    public async Task<bool> DeleteBundleManifestAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("Bundle ID cannot be empty", nameof(bundleId));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty", nameof(version));

        try
        {
            var blobName = GetBlobName(bundleId, version);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (response.Value)
            {
                _logger.LogInformation(
                    "Deleted bundle manifest {BundleId} version {Version}",
                    bundleId,
                    version);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete bundle manifest {BundleId} version {Version}",
                bundleId,
                version);
            throw;
        }
    }

    public async Task<bool> BundleManifestExistsAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            throw new ArgumentException("Bundle ID cannot be empty", nameof(bundleId));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty", nameof(version));

        try
        {
            var blobName = GetBlobName(bundleId, version);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check existence of bundle manifest {BundleId} version {Version}",
                bundleId,
                version);
            throw;
        }
    }

    /// <summary>
    /// Generates blob name from bundle ID and version.
    /// Structure: bundles/{bundleId}/{version}/manifest.json
    /// </summary>
    private static string GetBlobName(string bundleId, string version)
    {
        return $"bundles/{bundleId}/{version}/manifest.json";
    }
}
