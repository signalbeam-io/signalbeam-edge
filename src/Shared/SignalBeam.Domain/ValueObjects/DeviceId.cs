namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for devices.
/// </summary>
public readonly record struct DeviceId
{
    public Guid Value { get; init; }

    public DeviceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeviceId cannot be empty.", nameof(value));

        Value = value;
    }

    public static DeviceId New() => new(Guid.NewGuid());

    public static DeviceId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid DeviceId format: {value}");

        return new DeviceId(guid);
    }

    public static bool TryParse(string value, out DeviceId deviceId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            deviceId = new DeviceId(guid);
            return true;
        }

        deviceId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(DeviceId id) => id.Value;
}
