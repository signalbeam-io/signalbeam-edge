namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for device group memberships.
/// </summary>
public readonly record struct DeviceGroupMembershipId
{
    public Guid Value { get; init; }

    public DeviceGroupMembershipId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeviceGroupMembershipId cannot be empty.", nameof(value));

        Value = value;
    }

    public static DeviceGroupMembershipId New() => new(Guid.NewGuid());

    public static DeviceGroupMembershipId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid DeviceGroupMembershipId format: {value}");

        return new DeviceGroupMembershipId(guid);
    }

    public static bool TryParse(string value, out DeviceGroupMembershipId membershipId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            membershipId = new DeviceGroupMembershipId(guid);
            return true;
        }

        membershipId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(DeviceGroupMembershipId id) => id.Value;
}
