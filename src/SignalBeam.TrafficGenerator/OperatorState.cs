namespace SignalBeam.TrafficGenerator;

internal sealed record BundleInfo(string Name, List<string> Versions);

// What the operator has seen so far. Lists are kept sorted so seeded picks are repeatable.
internal sealed class OperatorState
{
    private const int MaxDevices = 200;

    public List<Guid> DeviceIds { get; private set; } = [];
    public List<Guid> GroupIds { get; set; } = [];
    public HashSet<Guid> TaggedDevices { get; } = [];
    public Dictionary<Guid, BundleInfo> Bundles { get; } = [];
    public Dictionary<Guid, string> Rollouts { get; set; } = [];
    public Dictionary<Guid, string> Alerts { get; } = [];

    public void MergeDevices(IEnumerable<Guid> ids) =>
        DeviceIds = DeviceIds.Union(ids).OrderBy(d => d).Take(MaxDevices).ToList();

    // 1.0.0 -> 1.0.1, using the highest version the bundle has.
    public static string NextVersion(IReadOnlyCollection<string> versions)
    {
        var latest = versions
            .Select(v => Version.TryParse(v, out var parsed) ? parsed : null)
            .Where(v => v is not null)
            .Max() ?? new Version(1, 0, 0);
        return $"{latest.Major}.{latest.Minor}.{Math.Max(latest.Build, 0) + 1}";
    }
}
