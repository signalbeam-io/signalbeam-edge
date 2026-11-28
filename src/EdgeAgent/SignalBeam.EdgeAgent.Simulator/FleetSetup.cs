using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignalBeam.EdgeAgent.Simulator;

// Registers the fleet and gives it the groups and tags that group/rollout queries need.
// Idempotent: re-running with the same seed finds the existing devices, groups and tags.
internal static class FleetSetup
{
    // Returns each device's own API key; devices send nothing with the operator's tenant key.
    public static async Task<IReadOnlyDictionary<Guid, string>> ProvisionAsync(
        HttpClient operatorClient,
        Guid tenantId,
        IReadOnlyList<DeviceSpec> devices,
        CancellationToken cancellationToken)
    {
        var deviceKeys = new Dictionary<Guid, string>();
        using (SimulatorTelemetry.Source.StartActivity("simulate fleet setup"))
        {
            var groupIds = await EnsureGroupsAsync(operatorClient, tenantId, cancellationToken);

            foreach (var device in devices)
            {
                await RegisterAsync(operatorClient, tenantId, device.DeviceId, device.Name, metadata: null, cancellationToken);
                deviceKeys[device.DeviceId] = await IssueDeviceKeyAsync(operatorClient, device.DeviceId, device.Name, cancellationToken);

                await SendAsync(
                    () => operatorClient.PutAsJsonAsync(
                        $"/api/devices/{device.DeviceId}/group",
                        new AssignDeviceToGroupRequest(device.DeviceId, groupIds[device.Environment]),
                        cancellationToken),
                    $"assign {device.Name} to group {device.Environment}",
                    cancellationToken);

                foreach (var tag in device.Tags)
                {
                    await SendAsync(
                        () => operatorClient.PostAsJsonAsync(
                            $"/api/devices/{device.DeviceId}/tags",
                            new AddDeviceTagRequest(device.DeviceId, tag),
                            cancellationToken),
                        $"tag {device.Name} with {tag}",
                        cancellationToken);
                }
            }
        }

        Console.WriteLine($"Fleet ready: {devices.Count} devices, "
            + string.Join(", ", devices.GroupBy(d => d.Environment).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}")) + ".");
        return deviceKeys;
    }

    // Approves a newly registered device, which issues its key. An already approved device gets a
    // fresh key with the old ones revoked, so reruns don't pile up keys (and the key from the last
    // run, which the simulator didn't keep, stops working).
    public static async Task<string> IssueDeviceKeyAsync(
        HttpClient operatorClient,
        Guid deviceId,
        string deviceName,
        CancellationToken cancellationToken)
    {
        using (var approve = await SendWithRateLimitRetryAsync(
                   () => operatorClient.PostAsync($"/api/devices/{deviceId}/approve", content: null, cancellationToken),
                   $"approve {deviceName}",
                   cancellationToken))
        {
            if (approve.IsSuccessStatusCode)
            {
                return await ReadKeyAsync(approve, deviceName, cancellationToken);
            }
        }

        using var generate = await SendWithRateLimitRetryAsync(
            () => operatorClient.PostAsJsonAsync(
                $"/api/devices/{deviceId}/api-keys",
                new GenerateDeviceApiKeyRequest(RevokeExistingKeys: true),
                cancellationToken),
            $"issue a key for {deviceName}",
            cancellationToken);

        if (!generate.IsSuccessStatusCode)
        {
            var content = await generate.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Could not issue an API key for {deviceName}: {generate.StatusCode} {content}");
        }

        return await ReadKeyAsync(generate, deviceName, cancellationToken);
    }

    private static async Task<string> ReadKeyAsync(HttpResponseMessage response, string deviceName, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<IssuedDeviceKeyResponse>(SimulatorJson.Options, cancellationToken);
        return !string.IsNullOrWhiteSpace(body?.ApiKey)
            ? body.ApiKey
            : throw new InvalidOperationException($"No API key returned for {deviceName}.");
    }

    // Returns false when the device was already registered.
    public static async Task<bool> RegisterAsync(
        HttpClient client,
        Guid tenantId,
        Guid deviceId,
        string deviceName,
        string? metadata,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRateLimitRetryAsync(
            () => client.PostAsJsonAsync(
                "/api/devices",
                new RegisterDeviceRequest(tenantId, deviceId, deviceName, metadata),
                cancellationToken),
            $"register {deviceName}",
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (content.Contains("DEVICE_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"Device registration failed for {deviceName}: {response.StatusCode} {content}");
    }

    private static async Task<Dictionary<string, Guid>> EnsureGroupsAsync(
        HttpClient client,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        using var listResponse = await SendWithRateLimitRetryAsync(
            () => client.GetAsync($"/api/groups?tenantId={tenantId}", cancellationToken),
            "list groups",
            cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var existing = await listResponse.Content.ReadFromJsonAsync<GetDeviceGroupsResponse>(
            SimulatorJson.Options, cancellationToken);

        var groupIds = (existing?.Groups ?? [])
            .GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var environment in FleetPlan.Environments.Where(e => !groupIds.ContainsKey(e)))
        {
            using var response = await SendWithRateLimitRetryAsync(
                () => client.PostAsJsonAsync(
                    "/api/groups",
                    new CreateDeviceGroupRequest(tenantId, environment, "Simulated fleet"),
                    cancellationToken),
                $"create group {environment}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Creating group {environment} failed: {response.StatusCode} {content}");
            }

            var created = await response.Content.ReadFromJsonAsync<CreateDeviceGroupResponse>(
                SimulatorJson.Options, cancellationToken);
            groupIds[environment] = created!.DeviceGroupId;
        }

        return groupIds;
    }

    private static async Task SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string label,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRateLimitRetryAsync(send, label, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Fleet setup: {label} failed: {response.StatusCode} {content}");
        }
    }

    // DeviceManager limits registrations per client IP (10/min by default) and all calls per tenant.
    // Setup must finish, so it waits out 429s instead of failing; that is also what a real
    // fleet coming online would do.
    internal const int MaxAttempts = 30;
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    internal static async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        string label,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;

        for (var attempt = 1; ; attempt++)
        {
            var response = await send();
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxAttempts)
            {
                return response;
            }

            TimeSpan wait;
            try
            {
                wait = await GetRetryAfterAsync(response, cancellationToken);
            }
            finally
            {
                response.Dispose();
            }

            Console.WriteLine($"Fleet setup: {label} rate limited; retrying in {wait.TotalSeconds:F0}s.");
            await delay(wait, cancellationToken);
        }
    }

    internal static async Task<TimeSpan> GetRetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // DeviceManager puts the delay in the body ({"retryAfter": seconds or null}); honour the header too.
        TimeSpan? wait = null;
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty("retryAfter", out var value)
                && value.ValueKind == JsonValueKind.Number)
            {
                wait = TimeSpan.FromSeconds(Math.Ceiling(value.GetDouble()));
            }
        }
        catch (JsonException)
        {
        }

        wait ??= response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;
        return TimeSpan.FromTicks(Math.Clamp(wait.Value.Ticks, TimeSpan.TicksPerSecond, MaxRetryDelay.Ticks));
    }
}
