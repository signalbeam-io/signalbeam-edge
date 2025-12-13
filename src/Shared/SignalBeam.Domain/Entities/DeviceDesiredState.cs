using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents the desired state for a device (what bundle version should be running).
/// This is set by the cloud and communicated to the edge agent.
/// </summary>
public class DeviceDesiredState : Entity<Guid>
{
    /// <summary>
    /// Device this desired state belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Desired bundle to run on the device.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Desired bundle version.
    /// </summary>
    public BundleVersion Version { get; private set; }

    /// <summary>
    /// When this desired state was set (UTC).
    /// </summary>
    public DateTimeOffset SetAt { get; private set; }

    /// <summary>
    /// When the desired state was last updated (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Container specifications as JSON.
    /// </summary>
    public string? ContainerSpecsJson { get; private set; }

    // EF Core constructor
    private DeviceDesiredState() : base()
    {
        Version = null!;
    }

    private DeviceDesiredState(
        Guid id,
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion version,
        DateTimeOffset setAt) : base(id)
    {
        DeviceId = deviceId;
        BundleId = bundleId;
        Version = version;
        SetAt = setAt;
        UpdatedAt = setAt;
    }

    /// <summary>
    /// Factory method to create a new desired state.
    /// </summary>
    public static DeviceDesiredState Create(
        DeviceId deviceId,
        BundleId bundleId,
        BundleVersion version,
        DateTimeOffset setAt,
        string? containerSpecsJson = null)
    {
        return new DeviceDesiredState(Guid.NewGuid(), deviceId, bundleId, version, setAt)
        {
            ContainerSpecsJson = containerSpecsJson
        };
    }

    /// <summary>
    /// Updates the desired state to a new version.
    /// </summary>
    public void UpdateVersion(BundleVersion version, DateTimeOffset updatedAt, string? containerSpecsJson = null)
    {
        Version = version;
        UpdatedAt = updatedAt;
        ContainerSpecsJson = containerSpecsJson;
    }
}
