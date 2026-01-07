using System.Text.RegularExpressions;

namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// URL-friendly tenant identifier (e.g., "acme-corp").
/// </summary>
public readonly record struct TenantSlug
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9-]{1,63}$", RegexOptions.Compiled);

    public string Value { get; init; }

    public TenantSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tenant slug cannot be empty.", nameof(value));

        var normalizedValue = value.ToLowerInvariant().Trim();

        if (!IsValid(normalizedValue))
            throw new ArgumentException(
                "Tenant slug must be 2-64 characters long and contain only lowercase letters, numbers, and hyphens. " +
                "It must start with a letter or number.",
                nameof(value));

        Value = normalizedValue;
    }

    private static bool IsValid(string slug) => SlugRegex.IsMatch(slug);

    public static TenantSlug Parse(string value) => new(value);

    public static bool TryParse(string value, out TenantSlug slug)
    {
        try
        {
            slug = new TenantSlug(value);
            return true;
        }
        catch
        {
            slug = default;
            return false;
        }
    }

    public override string ToString() => Value;

    public static implicit operator string(TenantSlug slug) => slug.Value;
}
