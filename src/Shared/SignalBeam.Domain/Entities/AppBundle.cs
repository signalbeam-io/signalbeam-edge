using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// App bundle representing a deployable application package with containers.
/// </summary>
public class AppBundle : AggregateRoot<BundleId>
{
    /// <summary>
    /// Tenant this bundle belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Name of the bundle.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Description of what this bundle does.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// When the bundle was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Latest version of this bundle.
    /// </summary>
    public BundleVersion? LatestVersion { get; private set; }

    // EF Core constructor
    private AppBundle() : base()
    {
    }

    private AppBundle(
        BundleId id,
        TenantId tenantId,
        string name,
        DateTimeOffset createdAt) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a new bundle.
    /// </summary>
    public static AppBundle Create(
        BundleId id,
        TenantId tenantId,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bundle name cannot be empty.", nameof(name));

        return new AppBundle(id, tenantId, name, createdAt)
        {
            Description = description
        };
    }

    /// <summary>
    /// Updates the bundle name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bundle name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// Updates the bundle description.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Updates the latest version.
    /// </summary>
    public void UpdateLatestVersion(BundleVersion version)
    {
        LatestVersion = version;
    }
}
