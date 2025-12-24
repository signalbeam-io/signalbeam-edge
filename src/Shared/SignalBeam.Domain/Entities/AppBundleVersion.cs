using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Versioned specification of an app bundle with container definitions.
/// </summary>
public class AppBundleVersion : Entity<Guid>
{
    /// <summary>
    /// Bundle this version belongs to.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Semantic version number.
    /// </summary>
    public BundleVersion Version { get; private set; }

    /// <summary>
    /// Container specifications for this version.
    /// </summary>
    public IReadOnlyList<ContainerSpec> Containers { get; private set; }

    /// <summary>
    /// When this version was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Release notes for this version.
    /// </summary>
    public string? ReleaseNotes { get; private set; }

    /// <summary>
    /// URI to the bundle definition in Azure Blob Storage.
    /// </summary>
    public string? BlobStorageUri { get; private set; }

    /// <summary>
    /// SHA256 checksum of the bundle definition for integrity verification.
    /// </summary>
    public string? Checksum { get; private set; }

    /// <summary>
    /// Size of the bundle definition in bytes.
    /// </summary>
    public long? SizeBytes { get; private set; }

    /// <summary>
    /// Status of the bundle version (Draft, Published, Deprecated).
    /// </summary>
    public BundleStatus Status { get; private set; }

    // EF Core constructor
    private AppBundleVersion() : base()
    {
        BundleId = default;
        Version = null!;
        Containers = new List<ContainerSpec>();
        Status = BundleStatus.Draft;
    }

    private AppBundleVersion(
        Guid id,
        BundleId bundleId,
        BundleVersion version,
        IReadOnlyList<ContainerSpec> containers,
        DateTimeOffset createdAt) : base(id)
    {
        BundleId = bundleId;
        Version = version;
        Containers = containers;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a new bundle version.
    /// </summary>
    public static AppBundleVersion Create(
        Guid id,
        BundleId bundleId,
        BundleVersion version,
        IReadOnlyList<ContainerSpec> containers,
        string? releaseNotes,
        DateTimeOffset createdAt)
    {
        if (containers == null || containers.Count == 0)
            throw new ArgumentException("Bundle version must have at least one container.", nameof(containers));

        return new AppBundleVersion(id, bundleId, version, containers, createdAt)
        {
            ReleaseNotes = releaseNotes
        };
    }

    /// <summary>
    /// Updates release notes.
    /// </summary>
    public void UpdateReleaseNotes(string? releaseNotes)
    {
        ReleaseNotes = releaseNotes;
    }

    /// <summary>
    /// Sets the blob storage metadata for this bundle version.
    /// </summary>
    public void SetBlobStorageMetadata(string blobStorageUri, string checksum, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(blobStorageUri))
            throw new ArgumentException("Blob storage URI cannot be empty.", nameof(blobStorageUri));

        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum cannot be empty.", nameof(checksum));

        if (sizeBytes <= 0)
            throw new ArgumentException("Size must be positive.", nameof(sizeBytes));

        BlobStorageUri = blobStorageUri;
        Checksum = checksum;
        SizeBytes = sizeBytes;
    }

    /// <summary>
    /// Publishes the bundle version, making it available for deployment.
    /// </summary>
    public void Publish()
    {
        if (Status == BundleStatus.Published)
            throw new InvalidOperationException("Bundle version is already published.");

        if (string.IsNullOrWhiteSpace(BlobStorageUri))
            throw new InvalidOperationException("Cannot publish bundle version without blob storage metadata.");

        Status = BundleStatus.Published;
    }

    /// <summary>
    /// Marks the bundle version as deprecated.
    /// </summary>
    public void Deprecate()
    {
        if (Status == BundleStatus.Deprecated)
            throw new InvalidOperationException("Bundle version is already deprecated.");

        Status = BundleStatus.Deprecated;
    }

    /// <summary>
    /// Checks if the bundle version is published and ready for deployment.
    /// </summary>
    public bool IsPublished() => Status == BundleStatus.Published;

    /// <summary>
    /// Checks if the bundle version is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == BundleStatus.Deprecated;
}
