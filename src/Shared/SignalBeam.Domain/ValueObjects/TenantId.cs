namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for tenants (multi-tenancy support).
/// </summary>
public readonly record struct TenantId
{
    public Guid Value { get; init; }

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(value));

        Value = value;
    }

    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid TenantId format: {value}");

        return new TenantId(guid);
    }

    public static bool TryParse(string value, out TenantId tenantId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            tenantId = new TenantId(guid);
            return true;
        }

        tenantId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(TenantId id) => id.Value;
}
