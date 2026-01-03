using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a membership relationship between a device and a device group.
/// Supports both static (manual) and dynamic (tag-based) memberships.
/// </summary>
public class DeviceGroupMembership : Entity<DeviceGroupMembershipId>
{
    /// <summary>
    /// ID of the device group.
    /// </summary>
    public DeviceGroupId GroupId { get; private set; }

    /// <summary>
    /// ID of the device.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Type of membership (Static or Dynamic).
    /// </summary>
    public MembershipType Type { get; private set; }

    /// <summary>
    /// When the device was added to the group (UTC).
    /// </summary>
    public DateTimeOffset AddedAt { get; private set; }

    /// <summary>
    /// Who added the device to the group.
    /// For static memberships: user ID or username.
    /// For dynamic memberships: "system".
    /// </summary>
    public string AddedBy { get; private set; } = string.Empty;

    // EF Core constructor
    private DeviceGroupMembership() : base()
    {
    }

    private DeviceGroupMembership(
        DeviceGroupMembershipId id,
        DeviceGroupId groupId,
        DeviceId deviceId,
        MembershipType type,
        DateTimeOffset addedAt,
        string addedBy) : base(id)
    {
        GroupId = groupId;
        DeviceId = deviceId;
        Type = type;
        AddedAt = addedAt;
        AddedBy = addedBy;
    }

    /// <summary>
    /// Factory method to create a new static membership (manually added).
    /// </summary>
    public static DeviceGroupMembership CreateStatic(
        DeviceGroupMembershipId id,
        DeviceGroupId groupId,
        DeviceId deviceId,
        string addedBy,
        DateTimeOffset addedAt)
    {
        if (string.IsNullOrWhiteSpace(addedBy))
            throw new ArgumentException("AddedBy cannot be empty for static memberships.", nameof(addedBy));

        return new DeviceGroupMembership(
            id,
            groupId,
            deviceId,
            MembershipType.Static,
            addedAt,
            addedBy);
    }

    /// <summary>
    /// Factory method to create a new dynamic membership (automatically added by tag query).
    /// </summary>
    public static DeviceGroupMembership CreateDynamic(
        DeviceGroupMembershipId id,
        DeviceGroupId groupId,
        DeviceId deviceId,
        DateTimeOffset addedAt)
    {
        return new DeviceGroupMembership(
            id,
            groupId,
            deviceId,
            MembershipType.Dynamic,
            addedAt,
            "system");
    }
}
