namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for app bundles.
/// </summary>
public readonly record struct BundleId
{
    public Guid Value { get; init; }

    public BundleId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("BundleId cannot be empty.", nameof(value));

        Value = value;
    }

    public static BundleId New() => new(Guid.NewGuid());

    public static BundleId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid BundleId format: {value}");

        return new BundleId(guid);
    }

    public static bool TryParse(string value, out BundleId bundleId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            bundleId = new BundleId(guid);
            return true;
        }

        bundleId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(BundleId id) => id.Value;
}
