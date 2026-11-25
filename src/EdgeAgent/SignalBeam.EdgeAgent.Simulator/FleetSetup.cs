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

    public static async Task RegisterAsync(
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
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (content.Contains("DEVICE_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException($"Device registration failed for {deviceName}: {response.StatusCode} {content}");
    }

    private static async Task<Dictionary<string, Guid>> EnsureGroupsAsync(
        HttpClient client,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await client.GetFromJsonAsync<GetDeviceGroupsResponse>(
            $"/api/groups?tenantId={tenantId}", SimulatorJson.Options, cancellationToken);

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
    private static async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        string label,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;

        for (var attempt = 1; ; attempt++)
        {
            var response = await send();
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == maxAttempts)
            {
                return response;
            }

            var delay = await GetRetryAfterAsync(response, cancellationToken);
            response.Dispose();
            Console.WriteLine($"Fleet setup: {label} rate limited; retrying in {delay.TotalSeconds:F0}s.");
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static async Task<TimeSpan> GetRetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // DeviceManager puts the delay in the body ({"retryAfter": seconds}); honour the header too.
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (body.RootElement.TryGetProperty("retryAfter", out var value) && value.TryGetDouble(out var seconds))
            {
                return TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling(seconds)));
            }
        }
        catch (JsonException)
        {
        }

        return response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
    }
}
