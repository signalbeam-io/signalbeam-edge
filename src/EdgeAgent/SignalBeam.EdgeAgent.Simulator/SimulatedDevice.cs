using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignalBeam.EdgeAgent.Simulator;

internal sealed record DeviceLoopOptions(
    TimeSpan HeartbeatInterval,
    TimeSpan MetricsInterval,
    TimeSpan DesiredStateInterval,
    int Iterations,
    bool ReportState,
    double Jitter,
    bool LogSuccess,
    bool ReportStateOnlyOnChange = false);

// One simulated edge device: heartbeat, metrics and desired-state loops.
// Each loop has its own Random seeded from the device seed, so values and jitter are reproducible.
internal sealed class SimulatedDevice
{
    private readonly Guid _deviceId;
    private readonly string _deviceName;
    private readonly HttpClient _deviceClient;
    private readonly HttpClient? _bundleClient;
    private readonly DeviceLoopOptions _options;
    private readonly TimeSpan _startOffset;
    private readonly int _seed;
    private readonly KeyValuePair<string, object?>[] _tags;
    private string? _lastBundleKey;

    public SimulatedDevice(
        Guid deviceId,
        string deviceName,
        HttpClient deviceClient,
        HttpClient? bundleClient,
        DeviceLoopOptions options,
        TimeSpan startOffset,
        int seed,
        string? environment = null)
    {
        _deviceId = deviceId;
        _deviceName = deviceName;
        _deviceClient = deviceClient;
        _bundleClient = bundleClient;
        _options = options;
        _startOffset = startOffset;
        _seed = seed;
        _tags =
        [
            new("signalbeam.device.id", deviceId.ToString()),
            new("signalbeam.device.name", deviceName),
            new("signalbeam.device.environment", environment),
        ];
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_startOffset > TimeSpan.Zero)
            {
                await Task.Delay(_startOffset, cancellationToken);
            }

            var startedAt = DateTimeOffset.UtcNow;
            var tasks = new List<Task>
            {
                RunLoopAsync("heartbeat", _options.HeartbeatInterval, new Random(_seed), (_, ct) => SendHeartbeatAsync(ct), cancellationToken),
                RunLoopAsync("metrics", _options.MetricsInterval, new Random(_seed + 1), (rng, ct) => SendMetricsAsync(rng, startedAt, ct), cancellationToken),
            };

            if (_bundleClient is not null)
            {
                tasks.Add(RunLoopAsync("desired-state", _options.DesiredStateInterval, new Random(_seed + 2), (_, ct) => PollDesiredStateAsync(ct), cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunLoopAsync(
        string action,
        TimeSpan interval,
        Random rng,
        Func<Random, CancellationToken, Task> tick,
        CancellationToken cancellationToken)
    {
        // Fixed schedule like PeriodicTimer: request latency doesn't push later ticks back,
        // and ticks missed while a request was slow are skipped rather than fired in a burst.
        var nextDue = DateTimeOffset.UtcNow;

        for (var count = 1; !cancellationToken.IsCancellationRequested; count++)
        {
            await TickAsync(action, rng, tick, cancellationToken);

            if (_options.Iterations > 0 && count >= _options.Iterations)
            {
                return;
            }

            nextDue += Jittered(interval, rng);
            var now = DateTimeOffset.UtcNow;
            if (nextDue <= now)
            {
                nextDue = now;
                continue;
            }

            await Task.Delay(nextDue - now, cancellationToken);
        }
    }

    private async Task TickAsync(
        string action,
        Random rng,
        Func<Random, CancellationToken, Task> tick,
        CancellationToken cancellationToken)
    {
        // Each tick is its own root trace, so every device action shows up as a separate flow.
        Activity.Current = null;
        using var activity = SimulatorTelemetry.Source.StartActivity(
            $"simulate {action}", ActivityKind.Internal, parentContext: default, tags: _tags);

        try
        {
            await tick(rng, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A failed tick (unreachable gateway, HttpClient timeout, bad payload) must not stop the loop.
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            activity?.AddException(ex);
            Console.WriteLine($"[{_deviceName}] {action} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private TimeSpan Jittered(TimeSpan interval, Random rng) => Jitter(interval, rng, _options.Jitter);

    internal static TimeSpan Jitter(TimeSpan interval, Random rng, double jitter)
    {
        if (jitter <= 0)
        {
            return interval;
        }

        var factor = 1 + ((rng.NextDouble() * 2) - 1) * jitter;
        return interval * factor;
    }

    private Task SendHeartbeatAsync(CancellationToken cancellationToken) =>
        PostAsync(
            _deviceClient,
            $"/api/devices/{_deviceId}/heartbeat",
            new RecordHeartbeatRequest(_deviceId, DateTimeOffset.UtcNow),
            "heartbeat",
            cancellationToken);

    private Task SendMetricsAsync(Random rng, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var request = new UpdateDeviceMetricsRequest(
            _deviceId,
            DateTimeOffset.UtcNow,
            CpuUsage: 10 + rng.NextDouble() * 70,
            MemoryUsage: 20 + rng.NextDouble() * 60,
            DiskUsage: 5 + rng.NextDouble() * 40,
            UptimeSeconds: (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
            RunningContainers: rng.Next(1, 5),
            AdditionalMetrics: null);

        return PostAsync(_deviceClient, $"/api/devices/{_deviceId}/metrics", request, "metrics", cancellationToken);
    }

    private async Task PollDesiredStateAsync(CancellationToken cancellationToken)
    {
        using var response = await _bundleClient!.GetAsync($"/api/devices/{_deviceId}/desired-state", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, response.StatusCode.ToString());
            Console.WriteLine($"[{_deviceName}] desired-state fetch failed: {response.StatusCode} {content}");
            return;
        }

        var desired = (await response.Content.ReadFromJsonAsync<DeviceDesiredStateResponse>(
            SimulatorJson.Options, cancellationToken))?.DesiredState;
        if (desired is null)
        {
            return;
        }

        var bundleKey = $"{desired.BundleId}:{desired.BundleVersion}";
        var changed = !string.Equals(bundleKey, _lastBundleKey, StringComparison.Ordinal);
        if (changed)
        {
            Console.WriteLine($"[{_deviceName}] desired state updated to bundle {desired.BundleId} v{desired.BundleVersion}.");
            _lastBundleKey = bundleKey;
        }

        // A real agent reports once it has rolled out a new bundle; fleets do the same to stay
        // under DeviceManager's per-tenant rate limit. Single-device mode keeps reporting every poll.
        if (_options.ReportState && (changed || !_options.ReportStateOnlyOnChange))
        {
            await PostAsync(
                _deviceClient,
                $"/api/devices/{_deviceId}/state",
                new ReportDeviceStateRequest(_deviceId, BundleDeploymentStatus.Completed, DateTimeOffset.UtcNow),
                "state",
                cancellationToken);
        }
    }

    private async Task PostAsync<T>(
        HttpClient client,
        string path,
        T body,
        string label,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            if (_options.LogSuccess)
            {
                Console.WriteLine($"[{_deviceName}] sent {label} at {DateTimeOffset.UtcNow:O}.");
            }

            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, response.StatusCode.ToString());
        Console.WriteLine($"[{_deviceName}] {label} request failed: {response.StatusCode} {content}");
    }
}
