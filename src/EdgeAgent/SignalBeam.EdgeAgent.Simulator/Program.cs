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

    private static DeviceCredentials? _deviceCredentials;
    private static readonly SemaphoreSlim _credentialsLock = new(1, 1);

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
            Description = "API key to send in X-Api-Key header (legacy mode - uses shared tenant key)."
        };

        var useDeviceAuthOption = new Option<bool>("--use-device-auth", () => false)
        {
            Description = "Use device-specific authentication (requires approval workflow)."
        };

        var credentialsPathOption = new Option<string?>("--credentials-path")
        {
            Description = "Path to store device credentials (default: ./sim-credentials-{deviceId}.json)."
        };

        var waitForApprovalOption = new Option<bool>("--wait-for-approval", () => false)
        {
            Description = "Wait for device approval before starting simulation."
        };

        var approvalCheckIntervalOption = new Option<int>("--approval-check-interval", () => 10)
        {
            Description = "Interval in seconds to check for approval (when waiting)."
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
        rootCommand.AddOption(useDeviceAuthOption);
        rootCommand.AddOption(credentialsPathOption);
        rootCommand.AddOption(waitForApprovalOption);
        rootCommand.AddOption(approvalCheckIntervalOption);
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
            var useDeviceAuth = context.ParseResult.GetValueForOption(useDeviceAuthOption);
            var credentialsPath = context.ParseResult.GetValueForOption(credentialsPathOption);
            var waitForApproval = context.ParseResult.GetValueForOption(waitForApprovalOption);
            var approvalCheckInterval = context.ParseResult.GetValueForOption(approvalCheckIntervalOption);
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

            var resolvedTenantId = ResolveTenantId(tenantId, "SIM_TENANT_ID", "--tenant-id");
            var effectiveDeviceId = deviceId ?? Guid.NewGuid();
            var effectiveDeviceName = string.IsNullOrWhiteSpace(deviceName)
                ? $"sim-{effectiveDeviceId.ToString()[..8]}"
                : deviceName;

            // Determine authentication mode
            HttpClient deviceClient;
            HttpClient? bundleClient;

            var resolvedBundleUrl = ResolveOptionalOption(
                bundleOrchestratorUrl,
                "SIM_BUNDLE_ORCHESTRATOR_URL");

            if (useDeviceAuth)
            {
                Console.WriteLine("Using device-specific authentication mode.");
                var effectiveCredentialsPath = credentialsPath ?? $"./sim-credentials-{effectiveDeviceId}.json";

                await InitializeDeviceCredentialsAsync(
                    resolvedDeviceManagerUrl,
                    resolvedTenantId,
                    effectiveDeviceId,
                    effectiveDeviceName,
                    metadata,
                    effectiveCredentialsPath,
                    waitForApproval,
                    approvalCheckInterval);

                deviceClient = CreateClientWithDeviceAuth(resolvedDeviceManagerUrl);
                bundleClient = string.IsNullOrWhiteSpace(resolvedBundleUrl)
                    ? null
                    : CreateClientWithDeviceAuth(resolvedBundleUrl);
            }
            else
            {
                Console.WriteLine("Using legacy shared API key mode.");
                var resolvedApiKey = ResolveRequiredOption(
                    apiKey,
                    "SIM_API_KEY",
                    "--api-key");
                deviceClient = CreateClient(resolvedDeviceManagerUrl, resolvedApiKey);
                bundleClient = string.IsNullOrWhiteSpace(resolvedBundleUrl)
                    ? null
                    : CreateClient(resolvedBundleUrl, resolvedApiKey);
            }

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

    private static async Task InitializeDeviceCredentialsAsync(
        string deviceManagerUrl,
        Guid tenantId,
        Guid deviceId,
        string deviceName,
        string? metadata,
        string credentialsPath,
        bool waitForApproval,
        int approvalCheckInterval)
    {
        await _credentialsLock.WaitAsync();
        try
        {
            // Try to load existing credentials
            if (File.Exists(credentialsPath))
            {
                var json = await File.ReadAllTextAsync(credentialsPath);
                _deviceCredentials = JsonSerializer.Deserialize<DeviceCredentials>(json, JsonOptions);

                if (_deviceCredentials?.DeviceId == deviceId)
                {
                    Console.WriteLine($"Loaded existing credentials from {credentialsPath}.");

                    // If already approved with API key, we're done
                    if (_deviceCredentials.RegistrationStatus == "Approved" && !string.IsNullOrEmpty(_deviceCredentials.ApiKey))
                    {
                        Console.WriteLine("Device already approved with API key.");
                        return;
                    }

                    // If pending, check current status
                    if (_deviceCredentials.RegistrationStatus == "Pending")
                    {
                        Console.WriteLine("Device registration is pending. Checking current status...");
                        await CheckAndUpdateRegistrationStatusAsync(deviceManagerUrl, credentialsPath);

                        if (_deviceCredentials.RegistrationStatus == "Approved" && !string.IsNullOrEmpty(_deviceCredentials.ApiKey))
                        {
                            return;
                        }
                    }
                }
            }

            // Register device
            Console.WriteLine($"Registering device {deviceId} ({deviceName})...");
            using var registerClient = new HttpClient { BaseAddress = new Uri(deviceManagerUrl.TrimEnd('/')) };

            var registerRequest = new RegisterDeviceRequest(tenantId, deviceId, deviceName, metadata);
            var response = await registerClient.PostAsJsonAsync("/api/devices", registerRequest);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (!content.Contains("DEVICE_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Device registration failed: {response.StatusCode} {content}");
                }
                Console.WriteLine("Device already registered.");
            }

            // Get registration status
            var statusResponse = await registerClient.GetAsync($"/api/devices/{deviceId}/registration-status");
            if (!statusResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to get registration status: {statusResponse.StatusCode}");
            }

            var statusData = await statusResponse.Content.ReadFromJsonAsync<GetRegistrationStatusResponse>(JsonOptions);

            _deviceCredentials = new DeviceCredentials
            {
                DeviceId = deviceId,
                TenantId = tenantId,
                ApiKey = statusData?.ApiKey,
                ApiKeyExpiresAt = statusData?.ApiKeyExpiresAt,
                RegistrationStatus = statusData?.Status ?? "Pending"
            };

            await SaveCredentialsAsync(credentialsPath);
            Console.WriteLine($"Device registration status: {_deviceCredentials.RegistrationStatus}");

            // Wait for approval if requested
            if (waitForApproval && _deviceCredentials.RegistrationStatus == "Pending")
            {
                Console.WriteLine($"Waiting for device approval (checking every {approvalCheckInterval} seconds)...");
                Console.WriteLine("Press Ctrl+C to cancel.");

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                while (!cts.Token.IsCancellationRequested && _deviceCredentials.RegistrationStatus == "Pending")
                {
                    await Task.Delay(TimeSpan.FromSeconds(approvalCheckInterval), cts.Token);
                    await CheckAndUpdateRegistrationStatusAsync(deviceManagerUrl, credentialsPath);
                }

                if (_deviceCredentials.RegistrationStatus == "Approved")
                {
                    Console.WriteLine("✅ Device approved! API key received.");
                }
                else if (_deviceCredentials.RegistrationStatus == "Rejected")
                {
                    throw new InvalidOperationException("Device registration was rejected.");
                }
            }
        }
        finally
        {
            _credentialsLock.Release();
        }
    }

    private static async Task CheckAndUpdateRegistrationStatusAsync(string deviceManagerUrl, string credentialsPath)
    {
        if (_deviceCredentials == null)
        {
            return;
        }

        using var client = new HttpClient { BaseAddress = new Uri(deviceManagerUrl.TrimEnd('/')) };
        var response = await client.GetAsync($"/api/devices/{_deviceCredentials.DeviceId}/registration-status");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to check registration status: {response.StatusCode}");
            return;
        }

        var statusData = await response.Content.ReadFromJsonAsync<GetRegistrationStatusResponse>(JsonOptions);
        if (statusData == null)
        {
            return;
        }

        var statusChanged = _deviceCredentials.RegistrationStatus != statusData.Status;
        var apiKeyReceived = !string.IsNullOrEmpty(statusData.ApiKey) && statusData.ApiKey != _deviceCredentials.ApiKey;

        _deviceCredentials.RegistrationStatus = statusData.Status;

        if (apiKeyReceived)
        {
            _deviceCredentials.ApiKey = statusData.ApiKey;
            _deviceCredentials.ApiKeyExpiresAt = statusData.ApiKeyExpiresAt;
        }

        if (statusChanged || apiKeyReceived)
        {
            await SaveCredentialsAsync(credentialsPath);
            Console.WriteLine($"Registration status updated: {_deviceCredentials.RegistrationStatus}");
        }
    }

    private static async Task SaveCredentialsAsync(string credentialsPath)
    {
        if (_deviceCredentials == null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(_deviceCredentials, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(credentialsPath, json);

        // Set file permissions (Unix only)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(credentialsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Console.WriteLine($"Credentials saved to {credentialsPath}");
    }

    private static HttpClient CreateClientWithDeviceAuth(string baseUrl)
    {
        if (_deviceCredentials == null || string.IsNullOrEmpty(_deviceCredentials.ApiKey))
        {
            throw new InvalidOperationException("Device credentials not initialized or API key not available.");
        }

        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/'))
        };

        client.DefaultRequestHeaders.Add("X-API-Key", _deviceCredentials.ApiKey);

        // Check if API key is expiring soon
        if (_deviceCredentials.ApiKeyExpiresAt.HasValue)
        {
            var daysUntilExpiration = (_deviceCredentials.ApiKeyExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;
            if (daysUntilExpiration < 7)
            {
                Console.WriteLine($"⚠️ API key expires in {daysUntilExpiration:F1} days. Consider rotating the key.");
            }
        }

        return client;
    }

    private record DeviceCredentials
    {
        public Guid DeviceId { get; set; }
        public Guid TenantId { get; set; }
        public string? ApiKey { get; set; }
        public DateTimeOffset? ApiKeyExpiresAt { get; set; }
        public string RegistrationStatus { get; set; } = "Pending";
    }

    private record GetRegistrationStatusResponse(
        string Status,
        string? ApiKey = null,
        DateTimeOffset? ApiKeyExpiresAt = null);
}
