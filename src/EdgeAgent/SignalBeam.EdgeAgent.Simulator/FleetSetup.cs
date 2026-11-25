using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignalBeam.EdgeAgent.Simulator;

// Registers the fleet and gives it the groups and tags that group/rollout queries need.
// Idempotent: re-running with the same seed finds the existing devices, groups and tags.
internal static class FleetSetup
{
    public static async Task ProvisionAsync(
        HttpClient deviceClient,
        Guid tenantId,
        IReadOnlyList<DeviceSpec> devices,
        CancellationToken cancellationToken)
    {
        using (SimulatorTelemetry.Source.StartActivity("simulate fleet setup"))
        {
            var groupIds = await EnsureGroupsAsync(deviceClient, tenantId, cancellationToken);

            foreach (var device in devices)
            {
                await RegisterAsync(deviceClient, tenantId, device.DeviceId, device.Name, metadata: null, cancellationToken);

                await SendAsync(
                    () => deviceClient.PutAsJsonAsync(
                        $"/api/devices/{device.DeviceId}/group",
                        new AssignDeviceToGroupRequest(device.DeviceId, groupIds[device.Environment]),
                        cancellationToken),
                    $"assign {device.Name} to group {device.Environment}",
                    cancellationToken);

                foreach (var tag in device.Tags)
                {
                    await SendAsync(
                        () => deviceClient.PostAsJsonAsync(
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
