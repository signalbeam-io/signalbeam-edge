namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for user invitations.
/// </summary>
public readonly record struct InvitationId
{
    public Guid Value { get; init; }

    public InvitationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("InvitationId cannot be empty.", nameof(value));

        Value = value;
    }

    public static InvitationId New() => new(Guid.NewGuid());

    public static InvitationId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new FormatException($"Invalid InvitationId format: {value}");

        return new InvitationId(guid);
    }

    public static bool TryParse(string value, out InvitationId invitationId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            invitationId = new InvitationId(guid);
            return true;
        }

        invitationId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(InvitationId id) => id.Value;
}
