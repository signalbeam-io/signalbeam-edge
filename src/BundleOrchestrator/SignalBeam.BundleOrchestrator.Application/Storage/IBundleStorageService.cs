namespace SignalBeam.BundleOrchestrator.Application.Storage;

/// <summary>
/// Interface for bundle artifact storage operations.
/// </summary>
public interface IBundleStorageService
{
    /// <summary>
    /// Uploads a bundle manifest to blob storage.
    /// </summary>
    Task<string> UploadBundleManifestAsync(
        string tenantId,
        string bundleId,
        string version,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a SAS token URL for downloading a bundle manifest.
    /// This allows edge agents to download bundles securely without authentication.
    /// </summary>
    Task<string> GenerateBundleDownloadUrlAsync(
        string tenantId,
        string bundleId,
        string version,
        TimeSpan validity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a bundle manifest from blob storage.
    /// </summary>
    Task<Stream> DownloadBundleManifestAsync(
        string tenantId,
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bundle manifest from blob storage.
    /// </summary>
    Task<bool> DeleteBundleManifestAsync(
        string tenantId,
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bundle manifest exists in blob storage.
    /// </summary>
    Task<bool> BundleManifestExistsAsync(
        string tenantId,
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a bundle manifest (checksum, size).
    /// </summary>
    Task<BundleMetadata> GetBundleMetadataAsync(
        string tenantId,
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a bundle manifest with checksum calculation.
    /// </summary>
    Task<BundleMetadata> UploadBundleWithMetadataAsync(
        string tenantId,
        string bundleId,
        string version,
        Stream content,
        string? checksumOverride = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bundle metadata including storage URI, checksum, and size.
/// </summary>
public record BundleMetadata(
    string BlobStorageUri,
    string Checksum,
    long SizeBytes);
