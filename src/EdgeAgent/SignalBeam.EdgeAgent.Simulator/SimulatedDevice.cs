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

// What a device shares with the rest of the fleet: the NATS publisher (null = HTTP only),
// its injected fault (null = healthy) and the fleet clock that scenario timings are relative to.
internal sealed record DeviceRuntime(NatsTelemetryPublisher? Nats, DeviceFault? Fault, DateTimeOffset ClockStart)
{
    public static readonly DeviceRuntime HttpOnly = new(null, null, DateTimeOffset.UtcNow);

    public TimeSpan Elapsed => DateTimeOffset.UtcNow - ClockStart;
}

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
    private readonly DeviceRuntime _runtime;
    private string? _lastBundleKey;
    private string? _lastReportedOutcome;
    private volatile bool _reconciliationFailing;

    public SimulatedDevice(
        Guid deviceId,
        string deviceName,
        HttpClient deviceClient,
        HttpClient? bundleClient,
        DeviceLoopOptions options,
        TimeSpan startOffset,
        int seed,
        string? environment = null,
        DeviceRuntime? runtime = null)
    {
        _runtime = runtime ?? DeviceRuntime.HttpOnly;
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
        // An offline device sends nothing at all, so there is no trace either.
        var fault = _runtime.Fault;
        var elapsed = _runtime.Elapsed;
        if (fault?.IsOffline(elapsed) == true)
        {
            return;
        }

        // Each tick is its own root trace, so every device action shows up as a separate flow.
        Activity.Current = null;
        using var activity = SimulatorTelemetry.Source.StartActivity(
            $"simulate {action}", ActivityKind.Internal, parentContext: default, tags: _tags);

        if (fault?.IsActive(elapsed) == true)
        {
            // Lets trace analysis separate injected failures from organic ones.
            activity?.SetTag("signalbeam.sim.scenario", fault.Scenario);
        }

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

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        await PostAsync(
            _deviceClient,
            $"/api/devices/{_deviceId}/heartbeat",
            new RecordHeartbeatRequest(_deviceId, DateTimeOffset.UtcNow),
            "heartbeat",
            cancellationToken);

        if (_runtime.Nats is { } nats)
        {
            // TelemetryProcessor's error-rate rule counts statuses containing "Failed".
            await nats.PublishHeartbeatAsync(_deviceId, _reconciliationFailing ? "ReconciliationFailed" : "Online", cancellationToken);
        }
    }

    private async Task SendMetricsAsync(Random rng, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var normal = new MetricsSample(
            Cpu: 10 + rng.NextDouble() * 70,
            Memory: 20 + rng.NextDouble() * 60,
            Disk: 5 + rng.NextDouble() * 40);
        var sample = _runtime.Fault?.Apply(normal, _runtime.Elapsed, rng) ?? normal;
        var now = DateTimeOffset.UtcNow;
        var uptime = (long)(now - startedAt).TotalSeconds;
        var containers = rng.Next(1, 5);

        await PostAsync(
            _deviceClient,
            $"/api/devices/{_deviceId}/metrics",
            new UpdateDeviceMetricsRequest(_deviceId, now, sample.Cpu, sample.Memory, sample.Disk, uptime, containers, null),
            "metrics",
            cancellationToken);

        if (_runtime.Nats is { } nats)
        {
            await nats.PublishMetricsAsync(
                new NatsMetricsMessage(_deviceId, now, sample.Cpu, sample.Memory, sample.Disk, uptime, containers),
                cancellationToken);
        }
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

        var desired = await response.Content.ReadFromJsonAsync<DeviceDesiredStateResponse>(
            SimulatorJson.Options, cancellationToken);
        if (desired?.BundleId is null || desired.BundleVersion is null)
        {
            _reconciliationFailing = false;
            return;
        }

        var bundleKey = $"{desired.BundleId}:{desired.BundleVersion}";
        var changed = !string.Equals(bundleKey, _lastBundleKey, StringComparison.Ordinal);
        if (changed)
        {
            Console.WriteLine($"[{_deviceName}] desired state updated to bundle {desired.BundleId} v{desired.BundleVersion}.");
            _lastBundleKey = bundleKey;
        }

        // deploy-failure: the device can't roll out its desired bundle, like an agent whose image pull fails.
        var failed = _runtime.Fault?.FailsDeployments(_runtime.Elapsed) == true;
        _reconciliationFailing = failed;
        var outcome = $"{bundleKey}:{(failed ? "failed" : "succeeded")}";

        // A real agent reports once per rollout outcome; fleets do the same to stay under DeviceManager's
        // per-tenant rate limit, retrying on the next poll until both reports succeed.
        // Single-device mode keeps reporting every poll.
        var unreported = !string.Equals(outcome, _lastReportedOutcome, StringComparison.Ordinal);
        if (_options.ReportState && (unreported || !_options.ReportStateOnlyOnChange))
        {
            var stateReported = await PostAsync(
                _deviceClient,
                $"/api/devices/{_deviceId}/state",
                new ReportDeviceStateRequest(
                    _deviceId,
                    failed ? BundleDeploymentStatus.Failed : BundleDeploymentStatus.Completed,
                    DateTimeOffset.UtcNow),
                "state",
                cancellationToken);

            var reconciliationReported = await PostAsync(
                _bundleClient,
                $"/api/devices/{_deviceId}/reconciliation-status",
                new ReconciliationStatusRequest(
                    _deviceId,
                    failed ? "failed" : "succeeded",
                    desired.BundleId,
                    desired.BundleVersion,
                    DateTime.UtcNow,
                    [],
                    failed ? [$"Failed to pull image for bundle {desired.BundleId} v{desired.BundleVersion}: manifest unknown (simulated)"] : []),
                "reconciliation-status",
                cancellationToken);

            if (stateReported && reconciliationReported)
            {
                _lastReportedOutcome = outcome;
            }
        }
    }

    private async Task<bool> PostAsync<T>(
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

            return true;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, response.StatusCode.ToString());
        Console.WriteLine($"[{_deviceName}] {label} request failed: {response.StatusCode} {content}");
        return false;
    }
}
