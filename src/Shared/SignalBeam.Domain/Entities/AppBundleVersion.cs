using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a specific version of an app bundle with container specifications.
/// Each version is immutable once created.
/// </summary>
public class AppBundleVersion : Entity<Guid>
{
    private readonly List<ContainerSpec> _containerSpecs = [];

    /// <summary>
    /// Bundle this version belongs to.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Semantic version of this bundle.
    /// </summary>
    public BundleVersion Version { get; private set; }

    /// <summary>
    /// When this version was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Created by (user or system).
    /// </summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Release notes for this version.
    /// </summary>
    public string? ReleaseNotes { get; private set; }

    /// <summary>
    /// Container specifications for this bundle version.
    /// </summary>
    public IReadOnlyCollection<ContainerSpec> ContainerSpecs => _containerSpecs.AsReadOnly();

    /// <summary>
    /// Whether this version is marked as stable/production-ready.
    /// </summary>
    public bool IsStable { get; private set; }

    /// <summary>
    /// Artifact storage URL (e.g., Azure Blob Storage URL).
    /// </summary>
    public string? ArtifactStorageUrl { get; private set; }

    /// <summary>
    /// SHA-256 checksum of the bundle artifacts.
    /// </summary>
    public string? Checksum { get; private set; }

    // EF Core constructor
    private AppBundleVersion() : base()
    {
        Version = null!;
    }

    private AppBundleVersion(
        Guid id,
        BundleId bundleId,
        BundleVersion version,
        DateTimeOffset createdAt,
        string createdBy) : base(id)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty.", nameof(createdBy));

        BundleId = bundleId;
        Version = version;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        IsStable = false;
    }

    /// <summary>
    /// Factory method to create a new bundle version.
    /// </summary>
    public static AppBundleVersion Create(
        BundleId bundleId,
        BundleVersion version,
        DateTimeOffset createdAt,
        string createdBy,
        IEnumerable<ContainerSpec> containerSpecs,
        string? releaseNotes = null,
        string? artifactStorageUrl = null,
        string? checksum = null)
    {
        var bundleVersion = new AppBundleVersion(Guid.NewGuid(), bundleId, version, createdAt, createdBy)
        {
            ReleaseNotes = releaseNotes,
            ArtifactStorageUrl = artifactStorageUrl,
            Checksum = checksum
        };

        foreach (var spec in containerSpecs)
        {
            bundleVersion._containerSpecs.Add(spec);
        }

        return bundleVersion;
    }

    /// <summary>
    /// Marks this version as stable/production-ready.
    /// </summary>
    public void MarkAsStable()
    {
        IsStable = true;
    }

    /// <summary>
    /// Marks this version as unstable (e.g., after discovering issues).
    /// </summary>
    public void MarkAsUnstable()
    {
        IsStable = false;
    }

    /// <summary>
    /// Updates the artifact storage information.
    /// </summary>
    public void UpdateArtifactInfo(string artifactStorageUrl, string checksum)
    {
        if (string.IsNullOrWhiteSpace(artifactStorageUrl))
            throw new ArgumentException("Artifact storage URL cannot be empty.", nameof(artifactStorageUrl));

        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum cannot be empty.", nameof(checksum));

        ArtifactStorageUrl = artifactStorageUrl;
        Checksum = checksum;
    }
}
