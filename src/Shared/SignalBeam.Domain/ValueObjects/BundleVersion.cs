using SignalBeam.Domain.Abstractions;
using System.Text.RegularExpressions;

namespace SignalBeam.Domain.ValueObjects;

/// <summary>
/// Semantic version for app bundles (e.g., "1.2.3").
/// </summary>
public partial class BundleVersion : ValueObject
{
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
    public string? PreRelease { get; init; }

    private BundleVersion(int major, int minor, int patch, string? preRelease = null)
    {
        if (major < 0) throw new ArgumentException("Major version cannot be negative.", nameof(major));
        if (minor < 0) throw new ArgumentException("Minor version cannot be negative.", nameof(minor));
        if (patch < 0) throw new ArgumentException("Patch version cannot be negative.", nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public static BundleVersion Create(int major, int minor, int patch, string? preRelease = null)
    {
        return new BundleVersion(major, minor, patch, preRelease);
    }

    public static BundleVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new FormatException("Version string cannot be empty.");

        var match = VersionRegex().Match(version);
        if (!match.Success)
            throw new FormatException($"Invalid semantic version format: {version}");

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = int.Parse(match.Groups[3].Value);
        var preRelease = match.Groups[4].Success ? match.Groups[4].Value : null;

        return new BundleVersion(major, minor, patch, preRelease);
    }

    public static bool TryParse(string version, out BundleVersion? bundleVersion)
    {
        try
        {
            bundleVersion = Parse(version);
            return true;
        }
        catch
        {
            bundleVersion = null;
            return false;
        }
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(PreRelease)
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
        yield return PreRelease;
    }

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$")]
    private static partial Regex VersionRegex();
}
