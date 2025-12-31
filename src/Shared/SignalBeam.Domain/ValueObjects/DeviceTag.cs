using System.Text.RegularExpressions;
using SignalBeam.Domain.Abstractions;

namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Represents a device tag with optional key-value structure.
/// Supports both simple tags ("production") and key-value tags ("environment=production").
/// </summary>
public sealed class DeviceTag : ValueObject
{
    /// <summary>
    /// The tag key. For simple tags, this equals the full tag value.
    /// For key-value tags, this is the part before '='.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The tag value. For simple tags, this equals the key.
    /// For key-value tags, this is the part after '='.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// True if this is a key-value tag (contains '='), false for simple tags.
    /// </summary>
    public bool IsKeyValue { get; }

    private DeviceTag(string key, string value, bool isKeyValue)
    {
        Key = key;
        Value = value;
        IsKeyValue = isKeyValue;
    }

    /// <summary>
    /// Creates a tag from a string. Parses "key=value" or simple "tag" format.
    /// </summary>
    /// <param name="tagString">Tag string to parse (e.g., "environment=production" or "production")</param>
    /// <returns>The parsed DeviceTag</returns>
    /// <exception cref="ArgumentException">Thrown if the tag string is invalid</exception>
    public static DeviceTag Create(string tagString)
    {
        if (string.IsNullOrWhiteSpace(tagString))
        {
            throw new ArgumentException("Tag cannot be empty.", nameof(tagString));
        }

        var normalized = tagString.Trim().ToLowerInvariant();

        // Check for key=value format
        var parts = normalized.Split('=', 2, StringSplitOptions.TrimEntries);

        if (parts.Length == 2)
        {
            // Key-value tag
            if (string.IsNullOrWhiteSpace(parts[0]))
            {
                throw new ArgumentException("Tag key cannot be empty.", nameof(tagString));
            }

            if (string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException("Tag value cannot be empty.", nameof(tagString));
            }

            // Validate key format (lowercase alphanumeric, hyphens, underscores)
            if (!IsValidTagComponent(parts[0]))
            {
                throw new ArgumentException(
                    "Tag key must contain only lowercase letters, numbers, hyphens, and underscores.",
                    nameof(tagString));
            }

            // Validate value format (same as key, but also allow wildcards)
            if (!IsValidTagComponent(parts[1], allowWildcard: true))
            {
                throw new ArgumentException(
                    "Tag value must contain only lowercase letters, numbers, hyphens, underscores, and wildcards (*).",
                    nameof(tagString));
            }

            return new DeviceTag(parts[0], parts[1], isKeyValue: true);
        }

        // Simple tag (backward compatibility)
        if (!IsValidTagComponent(normalized))
        {
            throw new ArgumentException(
                "Tag must contain only lowercase letters, numbers, hyphens, and underscores.",
                nameof(tagString));
        }

        return new DeviceTag(normalized, normalized, isKeyValue: false);
    }

    /// <summary>
    /// Checks if this tag matches the given key and pattern (with optional wildcard).
    /// </summary>
    /// <param name="key">Key to match (case-insensitive)</param>
    /// <param name="pattern">Value pattern to match (supports * wildcard)</param>
    /// <returns>True if the tag matches the key and pattern</returns>
    public bool Matches(string key, string pattern)
    {
        var normalizedKey = key.ToLowerInvariant();

        // For simple tags, match against the tag value
        if (!IsKeyValue)
        {
            return MatchesPattern(Value, pattern);
        }

        // For key-value tags, key must match exactly, then check value pattern
        if (!Key.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesPattern(Value, pattern);
    }

    /// <summary>
    /// Matches a value against a pattern with optional wildcard (*).
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="pattern">Pattern to match (supports * wildcard)</param>
    /// <returns>True if value matches pattern</returns>
    private static bool MatchesPattern(string value, string pattern)
    {
        var normalizedPattern = pattern.ToLowerInvariant();

        // No wildcard - exact match
        if (!normalizedPattern.Contains('*'))
        {
            return value.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        // Convert wildcard pattern to regex
        // Escape regex special chars except *
        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*", ".*") + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Validates a tag component (key or value).
    /// </summary>
    /// <param name="component">Component to validate</param>
    /// <param name="allowWildcard">Whether to allow wildcard (*) characters</param>
    /// <returns>True if component is valid</returns>
    private static bool IsValidTagComponent(string component, bool allowWildcard = false)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            return false;
        }

        // Must contain only lowercase letters, numbers, hyphens, underscores, and optionally wildcards
        var pattern = allowWildcard
            ? @"^[a-z0-9_\-*]+$"
            : @"^[a-z0-9_\-]+$";

        return Regex.IsMatch(component, pattern);
    }

    /// <summary>
    /// Returns the string representation of this tag.
    /// </summary>
    public override string ToString()
    {
        return IsKeyValue ? $"{Key}={Value}" : Value;
    }

    /// <summary>
    /// Gets the equality components for structural comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
        yield return Value;
        yield return IsKeyValue;
    }
}
