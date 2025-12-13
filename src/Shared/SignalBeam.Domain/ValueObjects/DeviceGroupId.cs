namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for device groups.
/// </summary>
public readonly record struct DeviceGroupId
{
    public Guid Value { get; init; }

    public DeviceGroupId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeviceGroupId cannot be empty.", nameof(value));

        Value = value;
    }

    public static DeviceGroupId New() => new(Guid.NewGuid());

    public static DeviceGroupId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid DeviceGroupId format: {value}");

        return new DeviceGroupId(guid);
    }

    public static bool TryParse(string value, out DeviceGroupId deviceGroupId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            deviceGroupId = new DeviceGroupId(guid);
            return true;
        }

        deviceGroupId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(DeviceGroupId id) => id.Value;
}
