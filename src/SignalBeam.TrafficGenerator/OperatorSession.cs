using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Models;
using SignalBeam.DeviceManager.Application.Commands;

namespace SignalBeam.TrafficGenerator;

// RunId goes into created names: the action sequence repeats per seed, names must not collide
// with an earlier run's bundles.
internal sealed record OperatorSettings(Guid TenantId, double InvalidRate, string RunId);

// One scripted operator. Each action is one span ("operator <action>") wrapping the HTTP calls
// it makes through the gateway, with the same API key and routes as the web UI.
internal sealed class OperatorSession
{
    public const string OperatorTag = "operator=checked";
    private const string BundlePrefix = "op-app-";

    private static readonly (string Image, string Tag)[] Images =
        [("nginx", "1.27"), ("redis", "7.4"), ("grafana/agent", "v0.43"), ("eclipse-mosquitto", "2.0")];
    private static readonly string[] DeviceStatuses = ["Online", "Offline", "Registered", "Error"];
    private static readonly string[] AlertStatuses = ["Active", "Acknowledged", "Resolved"];

    private readonly HttpClient _client;
    private readonly OperatorSettings _settings;
    private readonly ActionPicker _picker;
    private readonly OperatorState _state = new();

    public OperatorSession(HttpClient client, OperatorSettings settings, ActionPicker picker)
    {
        _client = client;
        _settings = settings;
        _picker = picker;
    }

    public OperatorState State => _state;

    private Random Random => _picker.Random;

    private string Tenant => $"tenantId={_settings.TenantId}";

    // Loads what the UI's first pages would show, so later actions have something to act on.
    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        await RunAsync(OperatorAction.ListDevices, cancellationToken);
        await RunAsync(OperatorAction.ListGroups, cancellationToken);
        await RunAsync(OperatorAction.ListBundles, cancellationToken);
        await RunAsync(OperatorAction.ListRollouts, cancellationToken);
        await RunAsync(OperatorAction.ListAlerts, cancellationToken);
    }

    public Task<HttpStatusCode?> NextAsync(CancellationToken cancellationToken) =>
        RunAsync(_picker.Next(), cancellationToken);

    public async Task<HttpStatusCode?> RunAsync(OperatorAction action, CancellationToken cancellationToken)
    {
        using var activity = TrafficTelemetry.Source.StartActivity($"operator {OperatorActions.Name(action)}");
        activity?.SetTag("signalbeam.operator.action", OperatorActions.Name(action));
        try
        {
            var status = await ExecuteAsync(action, cancellationToken);
            if (status is { } code)
            {
                activity?.SetTag("http.response.status_code", (int)code);
                if ((int)code >= 500)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, code.ToString());
                }
            }

            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            activity?.AddException(ex);
            return null;
        }
    }

    private Task<HttpStatusCode?> ExecuteAsync(OperatorAction action, CancellationToken ct) => action switch
    {
        OperatorAction.ListDevices => ListDevicesAsync($"/api/devices?{Tenant}&pageNumber={Random.Next(1, 3)}&pageSize=20", ct),
        OperatorAction.FilterDevices => ListDevicesAsync(FilterDevicesPath(), ct),
        OperatorAction.GetDevice => WithDevice(id => GetAsync($"/api/devices/{id}", ct), ct),
        OperatorAction.GetDeviceHealth => WithDevice(id => GetAsync($"/api/devices/{id}/health", ct), ct),
        OperatorAction.GetDeviceActivity => WithDevice(id => GetAsync($"/api/devices/{id}/activity-log?pageSize=20", ct), ct),
        OperatorAction.ListGroups => ListGroupsAsync(ct),
        OperatorAction.GetGroupMembers => With(_state.GroupIds, id => GetAsync($"/api/groups/{id}/memberships?{Tenant}", ct), ct),
        OperatorAction.ListTags => GetAsync($"/api/tags?{Tenant}", ct),
        OperatorAction.SearchDevicesByTag => GetAsync(
            $"/api/devices/search?{Tenant}&query={Uri.EscapeDataString(Pick(["environment=lab", "environment=prod-eu", "hardware=rpi", "environment=lab AND hardware=x86"]))}", ct),
        OperatorAction.AddDeviceTag => WithDevice(id => AddTagAsync(id, ct), ct),
        OperatorAction.RemoveDeviceTag => RemoveTagAsync(ct),
        OperatorAction.ListBundles => ListBundlesAsync(ct),
        OperatorAction.GetBundle => With(_state.Bundles.Keys.ToList(), id => GetAsync($"/api/bundles/{id}", ct), ct),
        OperatorAction.CreateBundle => CreateBundleAsync(ct),
        OperatorAction.CreateBundleVersion => CreateBundleVersionAsync(ct),
        OperatorAction.AssignBundle => AssignBundleAsync(ct),
        OperatorAction.ListRollouts => ListRolloutsAsync(ct),
        OperatorAction.GetActiveRollouts => GetAsync($"/api/phased-rollouts/active?{Tenant}", ct),
        OperatorAction.CreateRollout => CreateRolloutAsync(ct),
        OperatorAction.StartRollout => TransitionRolloutAsync("start", ["Pending"], "InProgress", ct),
        OperatorAction.PauseRollout => TransitionRolloutAsync("pause", ["InProgress"], "Paused", ct),
        OperatorAction.ResumeRollout => TransitionRolloutAsync("resume", ["Paused"], "InProgress", ct),
        OperatorAction.RollbackRollout => TransitionRolloutAsync("rollback", ["InProgress", "Paused"], "RolledBack", ct),
        OperatorAction.ListAlerts => ListAlertsAsync(ct),
        OperatorAction.GetAlertStatistics => GetAsync($"/api/alerts/statistics?{Tenant}", ct),
        OperatorAction.AcknowledgeAlert => AcknowledgeAlertAsync(ct),
        OperatorAction.ResolveAlert => ResolveAlertAsync(ct),
        OperatorAction.InvalidRequest => InvalidRequestAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private string FilterDevicesPath()
    {
        var filter = Random.Next(3) switch
        {
            0 => $"status={Pick(DeviceStatuses)}",
            1 => $"tag={Uri.EscapeDataString(Pick(["environment=lab", "environment=prod-eu", "hardware=rpi", "hardware=x86"]))}",
            _ => _state.GroupIds.Count > 0 ? $"deviceGroupId={Pick(_state.GroupIds)}" : "status=Online"
        };
        return $"/api/devices?{Tenant}&{filter}&pageSize=50";
    }

    private async Task<HttpStatusCode?> ListDevicesAsync(string path, CancellationToken ct)
    {
        var (status, body) = await GetJsonAsync(path, ct);
        if (body is { } json && json.TryGetProperty("devices", out var devices))
        {
            _state.MergeDevices(devices.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()));
        }

        return status;
    }

    private async Task<HttpStatusCode?> ListGroupsAsync(CancellationToken ct)
    {
        var (status, body) = await GetJsonAsync($"/api/groups?{Tenant}", ct);
        if (body is { } json && json.TryGetProperty("groups", out var groups))
        {
            _state.GroupIds = groups.EnumerateArray().Select(g => g.GetProperty("id").GetGuid()).OrderBy(g => g).ToList();
        }

        return status;
    }

    private async Task<HttpStatusCode?> AddTagAsync(Guid deviceId, CancellationToken ct)
    {
        var status = await SendAsync(HttpMethod.Post, $"/api/devices/{deviceId}/tags", new AddDeviceTagCommand(deviceId, OperatorTag), ct);
        if (status is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            _state.TaggedDevices.Add(deviceId);
        }

        return status;
    }

    private Task<HttpStatusCode?> RemoveTagAsync(CancellationToken ct) =>
        With(_state.TaggedDevices.OrderBy(d => d).ToList(), async id =>
        {
            var status = await SendAsync(HttpMethod.Delete, $"/api/tags/{id}/tags/{Uri.EscapeDataString(OperatorTag)}", null, ct);
            _state.TaggedDevices.Remove(id);
            return status;
        }, ct);

    private async Task<HttpStatusCode?> ListBundlesAsync(CancellationToken ct)
    {
        var (status, body) = await GetJsonAsync($"/api/bundles?{Tenant}", ct);
        if (body is { } json && json.TryGetProperty("bundles", out var bundles))
        {
            foreach (var bundle in bundles.EnumerateArray())
            {
                var versions = bundle.GetProperty("versions").EnumerateArray()
                    .Select(v => v.GetProperty("version").GetString()!)
                    .ToList();
                _state.Bundles[bundle.GetProperty("bundleId").GetGuid()] = new BundleInfo(
                    bundle.GetProperty("name").GetString()!, versions);
            }
        }

        return status;
    }

    private async Task<HttpStatusCode?> CreateBundleAsync(CancellationToken ct)
    {
        var name = $"{BundlePrefix}{_settings.RunId}-{Random.Next(1000, 9999)}";
        var (image, tag) = Pick(Images);
        // Same body as the web UI's create-bundle form (the endpoint's CreateBundleRequest).
        var request = new
        {
            name,
            description = "Created by the operator traffic generator",
            version = "1.0.0",
            containers = new[] { new { name = "app", image, tag } }
        };
        var (status, body) = await SendJsonAsync(HttpMethod.Post, $"/api/bundles?{Tenant}", request, ct);
        if (body is { } json && json.TryGetProperty("bundleId", out var id))
        {
            _state.Bundles[id.GetGuid()] = new BundleInfo(name, ["1.0.0"]);
        }

        return status;
    }

    // Only bumps bundles the generator created, so it never touches bundles a person made.
    private Task<HttpStatusCode?> CreateBundleVersionAsync(CancellationToken ct) =>
        With(OwnBundles(), async id =>
        {
            var bundle = _state.Bundles[id];
            var next = OperatorState.NextVersion(bundle.Versions);
            var (image, tag) = Pick(Images);
            var definition = new BundleDefinition
            {
                BundleId = id.ToString(),
                Version = next,
                Description = $"Operator release {next}",
                Containers = [new ContainerDefinition { Name = "app", Image = $"{image}:{tag}" }]
            };
            var status = await SendAsync(HttpMethod.Post, $"/api/bundles/{id}/versions?{Tenant}", definition, ct);
            if (status is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                bundle.Versions.Add(next);
            }

            return status;
        }, ct);

    private Task<HttpStatusCode?> AssignBundleAsync(CancellationToken ct) =>
        With(OwnBundles(), bundleId => WithDevice(deviceId =>
        {
            var command = new AssignBundleToDeviceCommand(
                deviceId.ToString(), bundleId.ToString(), _state.Bundles[bundleId].Versions[^1], "operator");
            return SendAsync(HttpMethod.Post, $"/api/devices/{deviceId}/bundle", command, ct);
        }, ct), ct);

    private async Task<HttpStatusCode?> ListRolloutsAsync(CancellationToken ct)
    {
        var (status, body) = await GetJsonAsync($"/api/phased-rollouts?{Tenant}&page=1&pageSize=50", ct);
        if (body is { } json && json.TryGetProperty("rollouts", out var rollouts))
        {
            _state.Rollouts = rollouts.EnumerateArray().ToDictionary(
                r => r.GetProperty("rolloutId").GetGuid(),
                r => r.GetProperty("status").GetString()!);
        }

        return status;
    }

    private Task<HttpStatusCode?> CreateRolloutAsync(CancellationToken ct) =>
        With(OwnBundles(), bundleId => With(_state.GroupIds, async groupId =>
        {
            var versions = _state.Bundles[bundleId].Versions;
            var command = new CreatePhasedRolloutCommand(
                _settings.TenantId,
                bundleId.ToString(),
                versions[^1],
                versions.Count > 1 ? versions[^2] : null,
                $"op-rollout-{_settings.RunId}-{Random.Next(1000, 9999)}",
                "Created by the operator traffic generator",
                groupId,
                [new PhaseConfigDto("canary", 10m, 1), new PhaseConfigDto("fleet", 100m)],
                CreatedBy: "operator");
            var (status, body) = await SendJsonAsync(HttpMethod.Post, "/api/phased-rollouts", command, ct);
            if (body is { } json && json.TryGetProperty("rolloutId", out var id))
            {
                _state.Rollouts[id.GetGuid()] = "Pending";
            }

            return status;
        }, ct), ct);

    private Task<HttpStatusCode?> TransitionRolloutAsync(string verb, string[] from, string to, CancellationToken ct)
    {
        var candidates = _state.Rollouts.Where(r => from.Contains(r.Value)).Select(r => r.Key).OrderBy(r => r).ToList();
        if (candidates.Count == 0)
        {
            // Nothing in the right state yet: do what the UI would, look at the list.
            return ListRolloutsAsync(ct);
        }

        return With(candidates, async id =>
        {
            var status = await SendAsync(HttpMethod.Post, $"/api/phased-rollouts/{id}/{verb}?{Tenant}", null, ct);
            if (status == HttpStatusCode.OK)
            {
                _state.Rollouts[id] = to;
            }

            return status;
        }, ct);
    }

    private async Task<HttpStatusCode?> ListAlertsAsync(CancellationToken ct)
    {
        var filter = Random.Next(2) == 0 ? "" : $"&status={Pick(AlertStatuses)}";
        var (status, body) = await GetJsonAsync($"/api/alerts?{Tenant}&limit=50{filter}", ct);
        if (body is { } json && json.TryGetProperty("alerts", out var alerts))
        {
            foreach (var alert in alerts.EnumerateArray())
            {
                _state.Alerts[alert.GetProperty("id").GetGuid()] = alert.GetProperty("status").ToString();
            }
        }

        return status;
    }

    private Task<HttpStatusCode?> AcknowledgeAlertAsync(CancellationToken ct) =>
        With(AlertsIn("Active", "0"), async id =>
        {
            var status = await SendAsync(HttpMethod.Post, $"/api/alerts/{id}/acknowledge?{Tenant}", new { acknowledgedBy = "operator" }, ct);
            _state.Alerts[id] = "Acknowledged";
            return status;
        }, ct);

    private Task<HttpStatusCode?> ResolveAlertAsync(CancellationToken ct) =>
        With(AlertsIn("Acknowledged", "1"), async id =>
        {
            var status = await SendAsync(HttpMethod.Post, $"/api/alerts/{id}/resolve?{Tenant}", null, ct);
            _state.Alerts.Remove(id);
            return status;
        }, ct);

    private Task<HttpStatusCode?> InvalidRequestAsync(CancellationToken ct) => Random.Next(5) switch
    {
        0 => GetAsync($"/api/devices/{Guid.NewGuid()}", ct),
        1 => SendAsync(HttpMethod.Post, $"/api/bundles?{Tenant}", new { name = "", containers = Array.Empty<object>() }, ct),
        2 => SendAsync(HttpMethod.Post, $"/api/alerts/{Guid.NewGuid()}/acknowledge?{Tenant}", new { acknowledgedBy = "operator" }, ct),
        3 => SendAsync(HttpMethod.Post, $"/api/phased-rollouts/{Guid.NewGuid()}/start?{Tenant}", null, ct),
        _ => GetAsync($"/api/bundles/{Guid.NewGuid()}", ct)
    };

    private List<Guid> OwnBundles() => _state.Bundles
        .Where(b => b.Value.Name.StartsWith(BundlePrefix, StringComparison.Ordinal))
        .Select(b => b.Key).OrderBy(b => b).ToList();

    private List<Guid> AlertsIn(string statusName, string statusNumber) => _state.Alerts
        .Where(a => a.Value == statusName || a.Value == statusNumber)
        .Select(a => a.Key).OrderBy(a => a).ToList();

    private Task<HttpStatusCode?> WithDevice(Func<Guid, Task<HttpStatusCode?>> action, CancellationToken ct) =>
        With(_state.DeviceIds, action, ct);

    // Runs the action on a seeded pick, or falls back to the device list when there's nothing to pick.
    private Task<HttpStatusCode?> With(IReadOnlyList<Guid> candidates, Func<Guid, Task<HttpStatusCode?>> action, CancellationToken ct) =>
        candidates.Count == 0 ? ListDevicesAsync($"/api/devices?{Tenant}&pageSize=50", ct) : action(Pick(candidates));

    private T Pick<T>(IReadOnlyList<T> items) => items[Random.Next(items.Count)];

    private async Task<HttpStatusCode?> GetAsync(string path, CancellationToken ct)
    {
        using var response = await _client.GetAsync(path, ct);
        return response.StatusCode;
    }

    private async Task<(HttpStatusCode?, JsonElement?)> GetJsonAsync(string path, CancellationToken ct)
    {
        using var response = await _client.GetAsync(path, ct);
        return (response.StatusCode, await ReadJsonAsync(response, ct));
    }

    private async Task<HttpStatusCode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var (status, _) = await SendJsonAsync(method, path, body, ct);
        return status;
    }

    private async Task<(HttpStatusCode?, JsonElement?)> SendJsonAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        using var response = await _client.SendAsync(request, ct);
        return (response.StatusCode, await ReadJsonAsync(response, ct));
    }

    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            return null;
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return document.RootElement.Clone();
    }
}
