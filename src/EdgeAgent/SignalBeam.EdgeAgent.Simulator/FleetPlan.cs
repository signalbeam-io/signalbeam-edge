namespace SignalBeam.EdgeAgent.Simulator;

public sealed record DeviceSpec(
    int Index,
    Guid DeviceId,
    string Name,
    string Environment,
    string Hardware,
    IReadOnlyList<string> Tags,
    double StartOffsetFraction,
    int Seed);

// Everything about the fleet comes from the seed, so a run can be reproduced exactly:
// same device IDs, names, groups, tags, start offsets and per-device jitter/metric sequences.
public static class FleetPlan
{
    public static readonly string[] Environments = ["lab", "prod-eu"];

    public static IReadOnlyList<DeviceSpec> Create(int fleetSize, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fleetSize, 1);

        var rng = new Random(seed);
        var devices = new List<DeviceSpec>(fleetSize);

        for (var i = 0; i < fleetSize; i++)
        {
            var deviceId = NextGuid(rng);

            // Roughly 1/3 lab, 2/3 production; 60% Raspberry Pis.
            var environment = rng.NextDouble() < 1.0 / 3 ? "lab" : "prod-eu";
            var hardware = rng.NextDouble() < 0.6 ? "rpi" : "x86";
            // key=value, so tag queries and dynamic groups can select them (e.g. "hardware=rpi").
            var tags = new List<string> { $"environment={environment}", $"hardware={hardware}" };

            devices.Add(new DeviceSpec(
                Index: i,
                DeviceId: deviceId,
                Name: $"sim-{environment}-{hardware}-{i + 1:D3}",
                Environment: environment,
                Hardware: hardware,
                Tags: tags,
                StartOffsetFraction: rng.NextDouble(),
                Seed: rng.Next()));
        }

        return devices;
    }

    private static Guid NextGuid(Random rng)
    {
        Span<byte> bytes = stackalloc byte[16];
        rng.NextBytes(bytes);

        // Mark as a version 4 / RFC 4122 variant GUID so it looks like any other device ID.
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
