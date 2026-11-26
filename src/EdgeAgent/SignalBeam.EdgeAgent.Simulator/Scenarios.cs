using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalBeam.EdgeAgent.Simulator;

public static class ScenarioNames
{
    public const string Offline = "offline";
    public const string Flapping = "flapping";
    public const string CpuSpike = "cpu-spike";
    public const string MemorySpike = "memory-spike";
    public const string DiskFill = "disk-fill";
    public const string DeployFailure = "deploy-failure";

    public static readonly string[] All = [Offline, Flapping, CpuSpike, MemorySpike, DiskFill, DeployFailure];

    // Spikes are transient by default; the others last until the simulator stops.
    internal static TimeSpan? DefaultDuration(string name) => name is CpuSpike or MemorySpike
        ? TimeSpan.FromMinutes(10)
        : null;
}

// Start is relative to when the fleet starts sending (after setup). A null duration lasts forever.
public sealed record ScenarioSpec(string Name, int Devices, TimeSpan Start, TimeSpan? Duration);

public sealed record DeviceFault(string Scenario, TimeSpan Start, TimeSpan? Duration)
{
    // Long enough on both sides for the server to notice: TelemetryProcessor's stale threshold
    // is 2 min, checked every minute; a returning heartbeat brings the device back at once.
    public static readonly TimeSpan FlapPeriod = TimeSpan.FromMinutes(8);

    public static readonly TimeSpan DiskFillRamp = TimeSpan.FromMinutes(5);

    public bool IsActive(TimeSpan elapsed) => elapsed >= Start && (Duration is null || elapsed < Start + Duration.Value);

    public bool IsOffline(TimeSpan elapsed)
    {
        if (!IsActive(elapsed))
        {
            return false;
        }

        return Scenario switch
        {
            ScenarioNames.Offline => true,
            // Offline for the first half of each period, online for the second.
            ScenarioNames.Flapping => (elapsed - Start).Ticks % FlapPeriod.Ticks < FlapPeriod.Ticks / 2,
            _ => false,
        };
    }

    public bool FailsDeployments(TimeSpan elapsed) => Scenario == ScenarioNames.DeployFailure && IsActive(elapsed);

    public MetricsSample Apply(MetricsSample normal, TimeSpan elapsed, Random rng)
    {
        if (!IsActive(elapsed))
        {
            return normal;
        }

        return Scenario switch
        {
            ScenarioNames.CpuSpike => normal with { Cpu = 96 + rng.NextDouble() * 3.5 },
            ScenarioNames.MemorySpike => normal with { Memory = 95 + rng.NextDouble() * 4.5 },
            ScenarioNames.DiskFill => normal with { Disk = FilledDisk(normal.Disk, elapsed - Start, rng) },
            _ => normal,
        };
    }

    // Grows linearly from the normal level to ~99% over DiskFillRamp, then stays full.
    private static double FilledDisk(double baseline, TimeSpan sinceStart, Random rng)
    {
        var progress = Math.Clamp(sinceStart / DiskFillRamp, 0, 1);
        var disk = baseline + (99 - baseline) * progress + rng.NextDouble() * 0.5;
        return Math.Min(disk, 99.9);
    }
}

public readonly record struct MetricsSample(double Cpu, double Memory, double Disk);

public static class ScenarioPlan
{
    // Picks distinct devices for each scenario from a seeded shuffle of the fleet, so the same
    // seed and scenarios always hit the same devices.
    public static IReadOnlyDictionary<Guid, DeviceFault> Assign(
        IReadOnlyList<DeviceSpec> fleet,
        IReadOnlyList<ScenarioSpec> scenarios,
        int seed)
    {
        var order = fleet.Select(d => d.DeviceId).ToArray();
        new Random(unchecked(seed * 7919 + 17)).Shuffle(order);

        var faults = new Dictionary<Guid, DeviceFault>();
        var next = 0;
        foreach (var scenario in scenarios)
        {
            for (var i = 0; i < scenario.Devices && next < order.Length; i++, next++)
            {
                faults[order[next]] = new DeviceFault(scenario.Name, scenario.Start, scenario.Duration);
            }
        }

        return faults;
    }

    public static int RequestedDevices(IReadOnlyList<ScenarioSpec> scenarios) => scenarios.Sum(s => s.Devices);

    public static IReadOnlyList<ScenarioSpec> FromFlags(
        IReadOnlyList<string> names,
        int devices,
        TimeSpan start,
        TimeSpan? duration) =>
        names
            .SelectMany(n => n.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(n => n.ToLowerInvariant())
            .Select(n => new ScenarioSpec(n, devices, start, duration ?? ScenarioNames.DefaultDuration(n)))
            .ToList();

    // {"scenarios":[{"name":"disk-fill","devices":2,"startSeconds":60,"durationSeconds":600}]}
    public static IReadOnlyList<ScenarioSpec> FromJson(string json, TimeSpan defaultStart)
    {
        var file = JsonSerializer.Deserialize<ScenarioFile>(json, JsonOptions)
            ?? throw new JsonException("Scenario file is empty.");

        return (file.Scenarios ?? [])
            .Select(s =>
            {
                var name = (s.Name ?? string.Empty).Trim().ToLowerInvariant();
                return new ScenarioSpec(
                    name,
                    s.Devices ?? 1,
                    s.StartSeconds is { } start ? TimeSpan.FromSeconds(start) : defaultStart,
                    s.DurationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : ScenarioNames.DefaultDuration(name));
            })
            .ToList();
    }

    public static string? Validate(IReadOnlyList<ScenarioSpec> scenarios, int fleetSize)
    {
        foreach (var scenario in scenarios)
        {
            if (!ScenarioNames.All.Contains(scenario.Name))
            {
                return $"Unknown scenario '{scenario.Name}'. Known: {string.Join(", ", ScenarioNames.All)}.";
            }

            if (scenario.Devices < 1 || scenario.Start < TimeSpan.Zero || scenario.Duration <= TimeSpan.Zero)
            {
                return $"Scenario '{scenario.Name}' needs at least 1 device, a start >= 0 and a positive duration.";
            }
        }

        if (scenarios.Count > 0 && fleetSize < 2)
        {
            return "Scenarios run in fleet mode; use --fleet-size 2 or more.";
        }

        var requested = RequestedDevices(scenarios);
        return requested > fleetSize
            ? $"Scenarios need {requested} devices but the fleet has {fleetSize}."
            : null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed record ScenarioFile([property: JsonPropertyName("scenarios")] List<ScenarioFileEntry>? Scenarios);

    private sealed record ScenarioFileEntry(string? Name, int? Devices, double? StartSeconds, double? DurationSeconds);
}
