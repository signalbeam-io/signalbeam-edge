using SignalBeam.Domain.Abstractions;
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

    // EF Core constructor
    private AppBundleVersion() : base()
    {
        BundleId = default;
        Version = null!;
        Containers = new List<ContainerSpec>();
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
}
