namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for users.
/// </summary>
public readonly record struct UserId
{
    public Guid Value { get; init; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(value));

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public static UserId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid UserId format: {value}");

        return new UserId(guid);
    }

    public static bool TryParse(string value, out UserId userId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            userId = new UserId(guid);
            return true;
        }

        userId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UserId id) => id.Value;
}
