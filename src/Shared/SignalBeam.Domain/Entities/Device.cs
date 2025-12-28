using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Device aggregate root representing an edge device (e.g., Raspberry Pi).
/// </summary>
public class Device : AggregateRoot<DeviceId>
{
    private readonly List<string> _tags = [];

    /// <summary>
    /// Tenant this device belongs to (multi-tenancy).
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Human-readable name for the device.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Current status of the device.
    /// </summary>
    public DeviceStatus Status { get; private set; }

    /// <summary>
    /// Last time the device sent a heartbeat (UTC).
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>
    /// When the device was registered (UTC).
    /// </summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>
    /// Device metadata (JSON stored as string, can be hardware info, location, etc.).
    /// </summary>
    public string? Metadata { get; private set; }

    /// <summary>
    /// Tags for device categorization (e.g., "lab", "prod", "rpi", "x86").
    /// </summary>
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Currently assigned bundle ID.
    /// </summary>
    public BundleId? AssignedBundleId { get; private set; }

    /// <summary>
    /// Deployment status of the assigned bundle.
    /// </summary>
    public BundleDeploymentStatus? BundleDeploymentStatus { get; private set; }

    /// <summary>
    /// Device group this device belongs to.
    /// </summary>
    public DeviceGroupId? DeviceGroupId { get; private set; }

    /// <summary>
    /// Registration approval status (Pending, Approved, Rejected).
    /// </summary>
    public DeviceRegistrationStatus RegistrationStatus { get; private set; }

    // EF Core constructor
    private Device() : base()
    {
    }

    private Device(
        DeviceId id,
        TenantId tenantId,
        string name,
        DateTimeOffset registeredAt,
        DeviceRegistrationStatus registrationStatus = DeviceRegistrationStatus.Pending) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Status = DeviceStatus.Registered;
        RegisteredAt = registeredAt;
        RegistrationStatus = registrationStatus;
    }

    /// <summary>
    /// Factory method to register a new device.
    /// </summary>
    public static Device Register(
        DeviceId id,
        TenantId tenantId,
        string name,
        DateTimeOffset registeredAt,
        string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Device name cannot be empty.", nameof(name));

        var device = new Device(id, tenantId, name, registeredAt)
        {
            Metadata = metadata
        };

        // Raise domain event
        device.RaiseDomainEvent(new DeviceRegisteredEvent(id, tenantId, name, registeredAt));

        return device;
    }

    /// <summary>
    /// Updates device name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Device name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// Records a heartbeat from the device.
    /// </summary>
    public void RecordHeartbeat(DateTimeOffset timestamp)
    {
        var wasOffline = Status == DeviceStatus.Offline;

        LastSeenAt = timestamp;

        if (Status == DeviceStatus.Registered || Status == DeviceStatus.Offline)
        {
            Status = DeviceStatus.Online;

            if (wasOffline)
            {
                RaiseDomainEvent(new DeviceOnlineEvent(Id, timestamp));
            }
        }
    }

    /// <summary>
    /// Marks the device as offline.
    /// </summary>
    public void MarkAsOffline(DateTimeOffset timestamp)
    {
        if (Status != DeviceStatus.Offline)
        {
            Status = DeviceStatus.Offline;
            RaiseDomainEvent(new DeviceOfflineEvent(Id, timestamp));
        }
    }

    /// <summary>
    /// Assigns a bundle to this device.
    /// </summary>
    public void AssignBundle(BundleId bundleId, DateTimeOffset assignedAt)
    {
        AssignedBundleId = bundleId;
        BundleDeploymentStatus = Enums.BundleDeploymentStatus.Pending;

        RaiseDomainEvent(new BundleAssignedEvent(Id, bundleId, assignedAt));
    }

    /// <summary>
    /// Updates the bundle deployment status.
    /// </summary>
    public void UpdateBundleDeploymentStatus(BundleDeploymentStatus status, DateTimeOffset timestamp)
    {
        BundleDeploymentStatus = status;

        if (status == Enums.BundleDeploymentStatus.InProgress)
        {
            Status = DeviceStatus.Updating;
        }
        else if (status == Enums.BundleDeploymentStatus.Completed)
        {
            Status = DeviceStatus.Online;
            RaiseDomainEvent(new BundleUpdateCompletedEvent(Id, AssignedBundleId!.Value, timestamp));
        }
        else if (status == Enums.BundleDeploymentStatus.Failed)
        {
            Status = DeviceStatus.Error;
            RaiseDomainEvent(new BundleUpdateFailedEvent(Id, AssignedBundleId!.Value, timestamp));
        }
    }

    /// <summary>
    /// Adds a tag to the device.
    /// </summary>
    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));

        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Removes a tag from the device.
    /// </summary>
    public void RemoveTag(string tag)
    {
        _tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Assigns device to a group.
    /// </summary>
    public void AssignToGroup(DeviceGroupId groupId)
    {
        DeviceGroupId = groupId;
    }

    /// <summary>
    /// Removes device from its group.
    /// </summary>
    public void RemoveFromGroup()
    {
        DeviceGroupId = null;
    }

    /// <summary>
    /// Updates device metadata.
    /// </summary>
    public void UpdateMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    /// <summary>
    /// Approves the device registration.
    /// </summary>
    public void ApproveRegistration(DateTimeOffset approvedAt)
    {
        if (RegistrationStatus == DeviceRegistrationStatus.Approved)
            throw new InvalidOperationException("Device registration is already approved.");

        if (RegistrationStatus == DeviceRegistrationStatus.Rejected)
            throw new InvalidOperationException("Cannot approve a rejected device registration.");

        RegistrationStatus = DeviceRegistrationStatus.Approved;
        RaiseDomainEvent(new DeviceRegistrationApprovedEvent(Id, approvedAt));
    }

    /// <summary>
    /// Rejects the device registration.
    /// </summary>
    public void RejectRegistration(DateTimeOffset rejectedAt, string? reason = null)
    {
        if (RegistrationStatus == DeviceRegistrationStatus.Rejected)
            throw new InvalidOperationException("Device registration is already rejected.");

        if (RegistrationStatus == DeviceRegistrationStatus.Approved)
            throw new InvalidOperationException("Cannot reject an approved device registration.");

        RegistrationStatus = DeviceRegistrationStatus.Rejected;
        RaiseDomainEvent(new DeviceRegistrationRejectedEvent(Id, rejectedAt, reason));
    }
}
