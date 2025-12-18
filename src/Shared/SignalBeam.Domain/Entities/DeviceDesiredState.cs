using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Desired state for a device - which bundle version it should be running.
/// </summary>
public class DeviceDesiredState : Entity<Guid>
{
    /// <summary>
    /// Device this desired state applies to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Bundle that should be deployed.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Specific version of the bundle.
    /// </summary>
    public BundleVersion BundleVersion { get; private set; }

    /// <summary>
    /// When this desired state was set (UTC).
    /// </summary>
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>
    /// Who or what assigned this bundle.
    /// </summary>
    public string? AssignedBy { get; private set; }

    // EF Core constructor
    private DeviceDesiredState() : base()
    {
        DeviceId = default;
        BundleId = default;
        BundleVersion = null!;
    }

    private DeviceDesiredState(
        Guid id,
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion bundleVersion,
        DateTimeOffset assignedAt) : base(id)
    {
        DeviceId = deviceId;
        BundleId = bundleId;
        BundleVersion = bundleVersion;
        AssignedAt = assignedAt;
    }

    /// <summary>
    /// Factory method to create desired state for a device.
    /// </summary>
    public static DeviceDesiredState Create(
        Guid id,
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion bundleVersion,
        string? assignedBy,
        DateTimeOffset assignedAt)
    {
        return new DeviceDesiredState(id, deviceId, bundleId, bundleVersion, assignedAt)
        {
            AssignedBy = assignedBy
        };
    }

    /// <summary>
    /// Updates the desired bundle version.
    /// </summary>
    public void UpdateBundleVersion(BundleVersion bundleVersion, string? assignedBy, DateTimeOffset assignedAt)
    {
        BundleVersion = bundleVersion;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }
}
