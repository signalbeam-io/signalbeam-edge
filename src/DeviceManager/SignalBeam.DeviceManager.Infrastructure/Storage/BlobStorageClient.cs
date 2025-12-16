using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace SignalBeam.DeviceManager.Infrastructure.Storage;

/// <summary>
/// Client for interacting with Azure Blob Storage.
/// Used for storing bundle artifacts and device-related files.
/// </summary>
public class BlobStorageClient : IBlobStorageClient
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageClient> _logger;
    private readonly string _containerName;

    public BlobStorageClient(
        BlobServiceClient blobServiceClient,
        ILogger<BlobStorageClient> logger,
        string containerName = "device-bundles")
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _containerName = containerName;
    }

    public async Task<string> UploadBlobAsync(
        string blobName,
        Stream content,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name cannot be empty", nameof(blobName));

        if (content == null)
            throw new ArgumentNullException(nameof(content));

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            await blobClient.UploadAsync(content, uploadOptions, cancellationToken);

            _logger.LogInformation(
                "Uploaded blob {BlobName} to container {ContainerName}",
                blobName,
                _containerName);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to upload blob {BlobName} to container {ContainerName}",
                blobName,
                _containerName);
            throw;
        }
    }

    public async Task<Stream> DownloadBlobAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name cannot be empty", nameof(blobName));

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Downloaded blob {BlobName} from container {ContainerName}",
                blobName,
                _containerName);

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to download blob {BlobName} from container {ContainerName}",
                blobName,
                _containerName);
            throw;
        }
    }

    public async Task<bool> DeleteBlobAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name cannot be empty", nameof(blobName));

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (response.Value)
            {
                _logger.LogInformation(
                    "Deleted blob {BlobName} from container {ContainerName}",
                    blobName,
                    _containerName);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete blob {BlobName} from container {ContainerName}",
                blobName,
                _containerName);
            throw;
        }
    }

    public async Task<bool> BlobExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name cannot be empty", nameof(blobName));

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check existence of blob {BlobName} in container {ContainerName}",
                blobName,
                _containerName);
            throw;
        }
    }
}

/// <summary>
/// Interface for blob storage operations.
/// </summary>
public interface IBlobStorageClient
{
    Task<string> UploadBlobAsync(
        string blobName,
        Stream content,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadBlobAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBlobAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> BlobExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default);
}
