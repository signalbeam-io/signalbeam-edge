namespace SignalBeam.BundleOrchestrator.Infrastructure.Storage;

/// <summary>
/// Interface for bundle artifact storage operations.
/// </summary>
public interface IBundleStorageService
{
    /// <summary>
    /// Uploads a bundle manifest to blob storage.
    /// </summary>
    Task<string> UploadBundleManifestAsync(
        string bundleId,
        string version,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a SAS token URL for downloading a bundle manifest.
    /// This allows edge agents to download bundles securely without authentication.
    /// </summary>
    Task<string> GenerateBundleDownloadUrlAsync(
        string bundleId,
        string version,
        TimeSpan validity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a bundle manifest from blob storage.
    /// </summary>
    Task<Stream> DownloadBundleManifestAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bundle manifest from blob storage.
    /// </summary>
    Task<bool> DeleteBundleManifestAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bundle manifest exists in blob storage.
    /// </summary>
    Task<bool> BundleManifestExistsAsync(
        string bundleId,
        string version,
        CancellationToken cancellationToken = default);
}
