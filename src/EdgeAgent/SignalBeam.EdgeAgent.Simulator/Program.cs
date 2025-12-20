using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignalBeam.EdgeAgent.Simulator;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SignalBeam Edge Agent Simulator");

        var deviceManagerUrlOption = new Option<string?>("--device-manager-url")
        {
            Description = "Base URL for DeviceManager API (ex: http://localhost:5001)."
        };

        var bundleOrchestratorUrlOption = new Option<string?>("--bundle-orchestrator-url")
        {
            Description = "Base URL for BundleOrchestrator API (optional)."
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description = "API key to send in X-Api-Key header."
        };

        var tenantIdOption = new Option<Guid?>("--tenant-id")
        {
            Description = "Tenant ID for device registration."
        };

        var deviceIdOption = new Option<Guid?>("--device-id")
        {
            Description = "Device ID to use (optional)."
        };

        var deviceNameOption = new Option<string?>("--device-name")
        {
            Description = "Device name to register (optional)."
        };

        var metadataOption = new Option<string?>("--metadata")
        {
            Description = "Optional metadata for registration."
        };

        var heartbeatIntervalOption = new Option<int>("--heartbeat-interval-seconds", () => 30)
        {
            Description = "Heartbeat interval in seconds."
        };

        var metricsIntervalOption = new Option<int>("--metrics-interval-seconds", () => 30)
        {
            Description = "Metrics interval in seconds."
        };

        var desiredStateIntervalOption = new Option<int>("--desired-state-interval-seconds", () => 60)
        {
            Description = "Desired state polling interval in seconds."
        };

        var iterationsOption = new Option<int>("--iterations", () => 0)
        {
            Description = "Number of loop iterations (0 = run until canceled)."
        };

        var reportStateOption = new Option<bool>("--report-state", () => true)
        {
            Description = "Report bundle deployment status when desired state is present."
        };

        rootCommand.AddOption(deviceManagerUrlOption);
        rootCommand.AddOption(bundleOrchestratorUrlOption);
        rootCommand.AddOption(apiKeyOption);
        rootCommand.AddOption(tenantIdOption);
        rootCommand.AddOption(deviceIdOption);
        rootCommand.AddOption(deviceNameOption);
        rootCommand.AddOption(metadataOption);
        rootCommand.AddOption(heartbeatIntervalOption);
        rootCommand.AddOption(metricsIntervalOption);
        rootCommand.AddOption(desiredStateIntervalOption);
        rootCommand.AddOption(iterationsOption);
        rootCommand.AddOption(reportStateOption);

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var deviceManagerUrl = context.ParseResult.GetValueForOption(deviceManagerUrlOption);
            var bundleOrchestratorUrl = context.ParseResult.GetValueForOption(bundleOrchestratorUrlOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption);
            var deviceId = context.ParseResult.GetValueForOption(deviceIdOption);
            var deviceName = context.ParseResult.GetValueForOption(deviceNameOption);
            var metadata = context.ParseResult.GetValueForOption(metadataOption);
            var heartbeatIntervalSeconds = context.ParseResult.GetValueForOption(heartbeatIntervalOption);
            var metricsIntervalSeconds = context.ParseResult.GetValueForOption(metricsIntervalOption);
            var desiredStateIntervalSeconds = context.ParseResult.GetValueForOption(desiredStateIntervalOption);
            var iterations = context.ParseResult.GetValueForOption(iterationsOption);
            var reportState = context.ParseResult.GetValueForOption(reportStateOption);

            var resolvedDeviceManagerUrl = ResolveRequiredOption(
                deviceManagerUrl,
                "SIM_DEVICE_MANAGER_URL",
                "--device-manager-url");

            var resolvedApiKey = ResolveRequiredOption(
                apiKey,
                "SIM_API_KEY",
                "--api-key");

            var resolvedTenantId = ResolveTenantId(tenantId, "SIM_TENANT_ID", "--tenant-id");
            var effectiveDeviceId = deviceId ?? Guid.NewGuid();
            var effectiveDeviceName = string.IsNullOrWhiteSpace(deviceName)
                ? $"sim-{effectiveDeviceId.ToString()[..8]}"
                : deviceName;

            using var deviceClient = CreateClient(resolvedDeviceManagerUrl, resolvedApiKey);
            var resolvedBundleUrl = ResolveOptionalOption(
                bundleOrchestratorUrl,
                "SIM_BUNDLE_ORCHESTRATOR_URL");
            using var bundleClient = string.IsNullOrWhiteSpace(resolvedBundleUrl)
                ? null
                : CreateClient(resolvedBundleUrl, resolvedApiKey);

            Console.WriteLine($"Simulator starting for device {effectiveDeviceId} ({effectiveDeviceName}).");
            Console.WriteLine($"DeviceManager: {resolvedDeviceManagerUrl}");
            Console.WriteLine(bundleClient is null
                ? "BundleOrchestrator: (disabled)"
                : $"BundleOrchestrator: {resolvedBundleUrl}");

            await RegisterDeviceAsync(deviceClient, resolvedTenantId, effectiveDeviceId, effectiveDeviceName, metadata);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            var startedAt = DateTimeOffset.UtcNow;
            var tasks = new List<Task>
            {
                RunHeartbeatLoop(deviceClient, effectiveDeviceId, heartbeatIntervalSeconds, iterations, cts.Token),
                RunMetricsLoop(deviceClient, effectiveDeviceId, startedAt, metricsIntervalSeconds, iterations, cts.Token)
            };

            if (bundleClient is not null)
            {
                tasks.Add(RunDesiredStateLoop(
                    bundleClient,
                    deviceClient,
                    effectiveDeviceId,
                    desiredStateIntervalSeconds,
                    iterations,
                    reportState,
                    cts.Token));
            }

            await Task.WhenAll(tasks);
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static HttpClient CreateClient(string baseUrl, string apiKey)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/'))
        };

        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static async Task RegisterDeviceAsync(
        HttpClient client,
        Guid tenantId,
        Guid deviceId,
        string deviceName,
        string? metadata)
    {
        var request = new RegisterDeviceRequest(tenantId, deviceId, deviceName, metadata);
        var response = await client.PostAsJsonAsync("/api/devices", request);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Device registration succeeded.");
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        if (content.Contains("DEVICE_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Device already registered; continuing.");
            return;
        }

        throw new InvalidOperationException($"Device registration failed: {response.StatusCode} {content}");
    }

    private static async Task RunHeartbeatLoop(
        HttpClient client,
        Guid deviceId,
        int intervalSeconds,
        int iterations,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
            var count = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var request = new RecordHeartbeatRequest(deviceId, DateTimeOffset.UtcNow);
                await SendRequestAsync(client, $"/api/devices/{deviceId}/heartbeat", request, "heartbeat");

                count++;
                if (iterations > 0 && count >= iterations)
                {
                    return;
                }

                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task RunMetricsLoop(
        HttpClient client,
        Guid deviceId,
        DateTimeOffset startedAt,
        int intervalSeconds,
        int iterations,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
            var count = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var uptimeSeconds = (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds;
                var request = new UpdateDeviceMetricsRequest(
                    deviceId,
                    DateTimeOffset.UtcNow,
                    CpuUsage: 10 + Random.Shared.NextDouble() * 70,
                    MemoryUsage: 20 + Random.Shared.NextDouble() * 60,
                    DiskUsage: 5 + Random.Shared.NextDouble() * 40,
                    UptimeSeconds: uptimeSeconds,
                    RunningContainers: Random.Shared.Next(1, 5),
                    AdditionalMetrics: null);

                await SendRequestAsync(client, $"/api/devices/{deviceId}/metrics", request, "metrics");

                count++;
                if (iterations > 0 && count >= iterations)
                {
                    return;
                }

                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task RunDesiredStateLoop(
        HttpClient bundleClient,
        HttpClient deviceClient,
        Guid deviceId,
        int intervalSeconds,
        int iterations,
        bool reportState,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
            var count = 0;
            string? lastBundleKey = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await bundleClient.GetAsync($"/api/devices/{deviceId}/desired-state", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var desiredState = await response.Content.ReadFromJsonAsync<DeviceDesiredStateResponse>(JsonOptions, cancellationToken);
                    var desired = desiredState?.DesiredState;

                    if (desired is not null)
                    {
                        var bundleKey = $"{desired.BundleId}:{desired.BundleVersion}";
                        if (!string.Equals(bundleKey, lastBundleKey, StringComparison.Ordinal))
                        {
                            Console.WriteLine($"Desired state updated to bundle {desired.BundleId} v{desired.BundleVersion}.");
                            lastBundleKey = bundleKey;
                        }

                        if (reportState)
                        {
                            var reportRequest = new ReportDeviceStateRequest(
                                deviceId,
                                BundleDeploymentStatus.Completed,
                                DateTimeOffset.UtcNow);

                            await SendRequestAsync(deviceClient, $"/api/devices/{deviceId}/state", reportRequest, "state");
                        }
                    }
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"Desired state fetch failed: {response.StatusCode} {content}");
                }

                count++;
                if (iterations > 0 && count >= iterations)
                {
                    return;
                }

                await timer.WaitForNextTickAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task SendRequestAsync<T>(
        HttpClient client,
        string path,
        T body,
        string label)
    {
        var response = await client.PostAsJsonAsync(path, body);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Sent {label} at {DateTimeOffset.UtcNow:O}.");
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{label} request failed: {response.StatusCode} {content}");
    }

    private record RegisterDeviceRequest(
        Guid TenantId,
        Guid DeviceId,
        string Name,
        string? Metadata);

    private record RecordHeartbeatRequest(
        Guid DeviceId,
        DateTimeOffset Timestamp);

    private record UpdateDeviceMetricsRequest(
        Guid DeviceId,
        DateTimeOffset Timestamp,
        double CpuUsage,
        double MemoryUsage,
        double DiskUsage,
        long UptimeSeconds,
        int RunningContainers,
        string? AdditionalMetrics);

    private record ReportDeviceStateRequest(
        Guid DeviceId,
        BundleDeploymentStatus? BundleDeploymentStatus,
        DateTimeOffset Timestamp);

    private record DeviceDesiredStateResponse(DeviceDesiredStateDto? DesiredState);

    private record DeviceDesiredStateDto(
        Guid DeviceId,
        Guid BundleId,
        string BundleVersion,
        DateTimeOffset AssignedAt,
        string? AssignedBy);

    private enum BundleDeploymentStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        RolledBack = 4
    }

    private static string ResolveRequiredOption(string? optionValue, string envName, string optionName)
    {
        if (!string.IsNullOrWhiteSpace(optionValue))
        {
            return optionValue;
        }

        var envValue = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        throw new ArgumentException($"Missing required option {optionName} or environment variable {envName}.");
    }

    private static string? ResolveOptionalOption(string? optionValue, string envName)
    {
        if (!string.IsNullOrWhiteSpace(optionValue))
        {
            return optionValue;
        }

        return Environment.GetEnvironmentVariable(envName);
    }

    private static Guid ResolveTenantId(Guid? optionValue, string envName, string optionName)
    {
        if (optionValue.HasValue)
        {
            return optionValue.Value;
        }

        var envValue = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(envValue) && Guid.TryParse(envValue, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Missing required option {optionName} or environment variable {envName}.");
    }
}
