using System.CommandLine;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace SignalBeam.EdgeAgent.Simulator;

public class Program
{
    // Fleet mode spreads each loop's interval by +/-10% so devices don't fire in lockstep.
    private const double FleetJitter = 0.1;

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
            Description = "Tenant API key for setup calls (register, approve, group, tag). Devices then use their own keys."
        };

        var useDeviceAuthOption = new Option<bool>("--use-device-auth")
        {
            DefaultValueFactory = _ => false,
            Description = "Use device-specific authentication (requires approval workflow)."
        };

        var useMtlsOption = new Option<bool>("--use-mtls")
        {
            DefaultValueFactory = _ => false,
            Description = "Request and use mTLS certificate after device approval (requires --use-device-auth)."
        };

        var credentialsPathOption = new Option<string?>("--credentials-path")
        {
            Description = "Path to store device credentials (default: ./sim-credentials-{deviceId}.json)."
        };

        var waitForApprovalOption = new Option<bool>("--wait-for-approval")
        {
            DefaultValueFactory = _ => false,
            Description = "Wait for device approval before starting simulation."
        };

        var approvalCheckIntervalOption = new Option<int>("--approval-check-interval")
        {
            DefaultValueFactory = _ => 10,
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

        var heartbeatIntervalOption = new Option<int>("--heartbeat-interval-seconds")
        {
            DefaultValueFactory = _ => 30,
            Description = "Heartbeat interval in seconds."
        };

        var metricsIntervalOption = new Option<int>("--metrics-interval-seconds")
        {
            DefaultValueFactory = _ => 30,
            Description = "Metrics interval in seconds."
        };

        var desiredStateIntervalOption = new Option<int>("--desired-state-interval-seconds")
        {
            DefaultValueFactory = _ => 60,
            Description = "Desired state polling interval in seconds."
        };

        var iterationsOption = new Option<int>("--iterations")
        {
            DefaultValueFactory = _ => 0,
            Description = "Number of loop iterations (0 = run until canceled)."
        };

        var reportStateOption = new Option<bool>("--report-state")
        {
            DefaultValueFactory = _ => true,
            Description = "Report bundle deployment status when desired state is present."
        };

        var fleetSizeOption = new Option<int>("--fleet-size")
        {
            DefaultValueFactory = _ => 1,
            Description = "Number of simulated devices (1 = single device, the original behaviour)."
        };

        var seedOption = new Option<int?>("--seed")
        {
            Description = "Seed for device IDs, names, groups, tags, timing jitter (+/-10%) and metric values. Random if omitted in fleet mode (printed at start)."
        };

        var natsUrlOption = new Option<string?>("--nats-url")
        {
            Description = "NATS server for heartbeats and metrics, like the real agent (or SIM_NATS_URL). Without it the simulator is HTTP only."
        };

        var scenarioOption = new Option<string[]>("--scenario")
        {
            Description = $"Failure scenario to inject; repeat or comma-separate. One of: {string.Join(", ", ScenarioNames.All)}.",
            AllowMultipleArgumentsPerToken = true
        };

        var scenarioDevicesOption = new Option<int>("--scenario-devices")
        {
            DefaultValueFactory = _ => 1,
            Description = "Devices affected by each --scenario (picked from the fleet by the seed)."
        };

        var scenarioStartOption = new Option<int>("--scenario-start")
        {
            DefaultValueFactory = _ => 120,
            Description = "Seconds after the fleet starts sending before --scenario failures begin."
        };

        var scenarioDurationOption = new Option<int?>("--scenario-duration")
        {
            Description = "Seconds each --scenario lasts (default: 10 min for spikes, until stopped otherwise)."
        };

        var scenarioFileOption = new Option<string?>("--scenario-file")
        {
            Description = "JSON file with scenarios: {\"scenarios\":[{\"name\":\"disk-fill\",\"devices\":2,\"startSeconds\":60}]}."
        };

        rootCommand.Options.Add(deviceManagerUrlOption);
        rootCommand.Options.Add(bundleOrchestratorUrlOption);
        rootCommand.Options.Add(apiKeyOption);
        rootCommand.Options.Add(useDeviceAuthOption);
        rootCommand.Options.Add(useMtlsOption);
        rootCommand.Options.Add(credentialsPathOption);
        rootCommand.Options.Add(waitForApprovalOption);
        rootCommand.Options.Add(approvalCheckIntervalOption);
        rootCommand.Options.Add(tenantIdOption);
        rootCommand.Options.Add(deviceIdOption);
        rootCommand.Options.Add(deviceNameOption);
        rootCommand.Options.Add(metadataOption);
        rootCommand.Options.Add(heartbeatIntervalOption);
        rootCommand.Options.Add(metricsIntervalOption);
        rootCommand.Options.Add(desiredStateIntervalOption);
        rootCommand.Options.Add(iterationsOption);
        rootCommand.Options.Add(reportStateOption);
        rootCommand.Options.Add(fleetSizeOption);
        rootCommand.Options.Add(seedOption);
        rootCommand.Options.Add(natsUrlOption);
        rootCommand.Options.Add(scenarioOption);
        rootCommand.Options.Add(scenarioDevicesOption);
        rootCommand.Options.Add(scenarioStartOption);
        rootCommand.Options.Add(scenarioDurationOption);
        rootCommand.Options.Add(scenarioFileOption);

        rootCommand.SetAction(async (parseResult, _) =>
        {
            var settings = new SimulatorSettings(
                DeviceManagerUrl: ResolveRequiredOption(
                    parseResult.GetValue(deviceManagerUrlOption), "SIM_DEVICE_MANAGER_URL", "--device-manager-url"),
                BundleOrchestratorUrl: ResolveOptionalOption(
                    parseResult.GetValue(bundleOrchestratorUrlOption), "SIM_BUNDLE_ORCHESTRATOR_URL"),
                ApiKey: parseResult.GetValue(apiKeyOption),
                TenantId: ResolveTenantId(parseResult.GetValue(tenantIdOption), "SIM_TENANT_ID", "--tenant-id"),
                Seed: parseResult.GetValue(seedOption),
                NatsUrl: ResolveOptionalOption(parseResult.GetValue(natsUrlOption), "SIM_NATS_URL"),
                Loop: new DeviceLoopOptions(
                    HeartbeatInterval: TimeSpan.FromSeconds(parseResult.GetValue(heartbeatIntervalOption)),
                    MetricsInterval: TimeSpan.FromSeconds(parseResult.GetValue(metricsIntervalOption)),
                    DesiredStateInterval: TimeSpan.FromSeconds(parseResult.GetValue(desiredStateIntervalOption)),
                    Iterations: parseResult.GetValue(iterationsOption),
                    ReportState: parseResult.GetValue(reportStateOption),
                    Jitter: 0,
                    LogSuccess: true));

            var fleetSize = parseResult.GetValue(fleetSizeOption);
            var useDeviceAuth = parseResult.GetValue(useDeviceAuthOption);
            var deviceId = parseResult.GetValue(deviceIdOption);
            var deviceName = parseResult.GetValue(deviceNameOption);

            var singleDeviceOptions = new[]
            {
                parseResult.GetResult(useDeviceAuthOption),
                parseResult.GetResult(useMtlsOption),
                parseResult.GetResult(credentialsPathOption),
                parseResult.GetResult(waitForApprovalOption),
                parseResult.GetResult(deviceIdOption),
                parseResult.GetResult(deviceNameOption),
                parseResult.GetResult(metadataOption),
            }
            .Where(r => r is { Implicit: false })
            .Select(r => r!.Option.Name)
            .ToList();

            var error = ValidateOptions(fleetSize, singleDeviceOptions, settings.Loop);
            IReadOnlyList<ScenarioSpec> scenarios = [];
            if (error is null)
            {
                (scenarios, error) = LoadScenarios(
                    parseResult.GetValue(scenarioFileOption),
                    parseResult.GetValue(scenarioOption) ?? [],
                    parseResult.GetValue(scenarioDevicesOption),
                    TimeSpan.FromSeconds(parseResult.GetValue(scenarioStartOption)),
                    parseResult.GetValue(scenarioDurationOption) is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
                    fleetSize);
            }

            if (error is not null)
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            using var host = SimulatorTelemetry.BuildHost();
            await host.StartAsync();
            var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

            try
            {
                if (fleetSize == 1)
                {
                    await RunSingleDeviceAsync(
                        settings,
                        deviceId,
                        deviceName,
                        parseResult.GetValue(metadataOption),
                        useDeviceAuth,
                        parseResult.GetValue(useMtlsOption),
                        parseResult.GetValue(credentialsPathOption),
                        parseResult.GetValue(waitForApprovalOption),
                        parseResult.GetValue(approvalCheckIntervalOption),
                        stopping);
                }
                else
                {
                    await RunFleetAsync(settings, fleetSize, scenarios, stopping);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                Console.WriteLine("Simulator stopped.");
            }
            finally
            {
                // Export spans still buffered in the batch processor before the process exits.
                host.Services.GetService<TracerProvider>()?.ForceFlush(5000);
                await host.StopAsync(CancellationToken.None);
            }

            return 0;
        });

        // The host's console lifetime owns Ctrl+C/SIGTERM so shutdown can finish and flush spans;
        // System.CommandLine's own handler would end the process after 2s.
        return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = null });
    }

    internal static string? ValidateOptions(int fleetSize, IReadOnlyCollection<string> singleDeviceOptions, DeviceLoopOptions loop)
    {
        if (fleetSize < 1)
        {
            return "--fleet-size must be at least 1.";
        }

        if (fleetSize > 1 && singleDeviceOptions.Count > 0)
        {
            return "--fleet-size > 1 uses the shared API key and generated devices; "
                + $"{string.Join(", ", singleDeviceOptions)} only apply to a single device.";
        }

        if (loop.HeartbeatInterval < TimeSpan.FromSeconds(1)
            || loop.MetricsInterval < TimeSpan.FromSeconds(1)
            || loop.DesiredStateInterval < TimeSpan.FromSeconds(1))
        {
            return "Heartbeat, metrics and desired-state intervals must be at least 1 second.";
        }

        return null;
    }

    internal static (IReadOnlyList<ScenarioSpec> Scenarios, string? Error) LoadScenarios(
        string? file,
        IReadOnlyList<string> names,
        int devices,
        TimeSpan start,
        TimeSpan? duration,
        int fleetSize)
    {
        var scenarios = new List<ScenarioSpec>();
        try
        {
            if (!string.IsNullOrWhiteSpace(file))
            {
                scenarios.AddRange(ScenarioPlan.FromJson(File.ReadAllText(file), start));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return ([], $"Could not read scenario file {file}: {ex.Message}");
        }

        scenarios.AddRange(ScenarioPlan.FromFlags(names, devices, start, duration));
        return (scenarios, ScenarioPlan.Validate(scenarios, fleetSize));
    }

    private sealed record SimulatorSettings(
        string DeviceManagerUrl,
        string? BundleOrchestratorUrl,
        string? ApiKey,
        Guid TenantId,
        int? Seed,
        string? NatsUrl,
        DeviceLoopOptions Loop);

    private static NatsTelemetryPublisher? CreateNatsPublisher(SimulatorSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NatsUrl))
        {
            return null;
        }

        Console.WriteLine($"NATS: {settings.NatsUrl}");
        return new NatsTelemetryPublisher(settings.NatsUrl);
    }

    private static async Task RunSingleDeviceAsync(
        SimulatorSettings settings,
        Guid? deviceId,
        string? deviceName,
        string? metadata,
        bool useDeviceAuth,
        bool useMtls,
        string? credentialsPath,
        bool waitForApproval,
        int approvalCheckInterval,
        CancellationToken cancellationToken)
    {
        // With a seed, the single device gets the ID and name of device 1 of a fleet with that seed
        // (but no group or tags; those come from fleet setup).
        var spec = settings.Seed is { } seed ? FleetPlan.Create(1, seed)[0] : null;
        var effectiveDeviceId = deviceId ?? spec?.DeviceId ?? Guid.NewGuid();
        var effectiveDeviceName = !string.IsNullOrWhiteSpace(deviceName)
            ? deviceName
            : deviceId is null && spec is not null ? spec.Name : $"sim-{effectiveDeviceId.ToString()[..8]}";

        HttpClient deviceClient;
        HttpClient? bundleClient;
        var bundleUrl = settings.BundleOrchestratorUrl;

        if (useDeviceAuth)
        {
            Console.WriteLine("Using device-specific authentication mode.");
            var effectiveCredentialsPath = credentialsPath ?? $"./sim-credentials-{effectiveDeviceId}.json";

            await InitializeDeviceCredentialsAsync(
                settings.DeviceManagerUrl,
                settings.TenantId,
                effectiveDeviceId,
                effectiveDeviceName,
                metadata,
                effectiveCredentialsPath,
                waitForApproval,
                approvalCheckInterval);

            if (useMtls)
            {
                await RequestCertificateAsync(settings.DeviceManagerUrl, effectiveDeviceId, effectiveCredentialsPath);
                deviceClient = CreateClientWithMtls(settings.DeviceManagerUrl);
                bundleClient = string.IsNullOrWhiteSpace(bundleUrl) ? null : CreateClientWithMtls(bundleUrl);
            }
            else
            {
                deviceClient = CreateClientWithDeviceAuth(settings.DeviceManagerUrl);
                bundleClient = string.IsNullOrWhiteSpace(bundleUrl) ? null : CreateClientWithDeviceAuth(bundleUrl);
            }
        }
        else
        {
            // The tenant key registers and approves the device; the device then uses its own key.
            Console.WriteLine("Using the tenant API key to set up the device.");
            using var operatorClient = CreateOperatorClient(settings);
            var registered = await FleetSetup.RegisterAsync(
                operatorClient, settings.TenantId, effectiveDeviceId, effectiveDeviceName, metadata, cancellationToken);
            Console.WriteLine(registered ? "Device registration succeeded." : "Device already registered; continuing.");

            var deviceKey = await FleetSetup.IssueDeviceKeyAsync(operatorClient, effectiveDeviceId, effectiveDeviceName, cancellationToken);
            var handler = new SocketsHttpHandler();
            (deviceClient, bundleClient) = CreateDeviceKeyClients(settings, handler, deviceKey);
        }

        Console.WriteLine($"Simulator starting for device {effectiveDeviceId} ({effectiveDeviceName}).");
        Console.WriteLine($"DeviceManager: {settings.DeviceManagerUrl}");
        Console.WriteLine(bundleClient is null ? "BundleOrchestrator: (disabled)" : $"BundleOrchestrator: {bundleUrl}");

        if (useDeviceAuth)
        {
            var registered = await FleetSetup.RegisterAsync(
                deviceClient, settings.TenantId, effectiveDeviceId, effectiveDeviceName, metadata, cancellationToken);
            Console.WriteLine(registered ? "Device registration succeeded." : "Device already registered; continuing.");
        }

        await using var nats = CreateNatsPublisher(settings);
        var loop = spec is null ? settings.Loop : settings.Loop with { Jitter = FleetJitter };
        var device = new SimulatedDevice(
            effectiveDeviceId,
            effectiveDeviceName,
            deviceClient,
            bundleClient,
            loop,
            TimeSpan.Zero,
            spec?.Seed ?? Random.Shared.Next(),
            runtime: new DeviceRuntime(nats, null, DateTimeOffset.UtcNow));

        await device.RunAsync(cancellationToken);
    }

    private static async Task RunFleetAsync(
        SimulatorSettings settings,
        int fleetSize,
        IReadOnlyList<ScenarioSpec> scenarios,
        CancellationToken cancellationToken)
    {
        var seed = settings.Seed ?? Random.Shared.Next();
        var plan = FleetPlan.Create(fleetSize, seed);
        using var operatorClient = CreateOperatorClient(settings);

        Console.WriteLine($"Simulating a fleet of {fleetSize} devices (seed {seed}; rerun with --seed {seed} to reproduce).");
        Console.WriteLine($"DeviceManager: {settings.DeviceManagerUrl}");
        Console.WriteLine(string.IsNullOrWhiteSpace(settings.BundleOrchestratorUrl)
            ? "BundleOrchestrator: (disabled)"
            : $"BundleOrchestrator: {settings.BundleOrchestratorUrl}");

        await using var nats = CreateNatsPublisher(settings);
        var deviceKeys = await FleetSetup.ProvisionAsync(operatorClient, settings.TenantId, plan, cancellationToken);

        // Each device authenticates with its own key; the clients share one connection pool.
        using var handler = new SocketsHttpHandler();
        var clients = plan.ToDictionary(spec => spec.DeviceId, spec => CreateDeviceKeyClients(settings, handler, deviceKeys[spec.DeviceId]));

        var faults = ScenarioPlan.Assign(plan, scenarios, seed);
        foreach (var spec in plan.Where(d => faults.ContainsKey(d.DeviceId)))
        {
            var fault = faults[spec.DeviceId];
            Console.WriteLine($"Scenario {fault.Scenario}: {spec.Name} ({spec.DeviceId}) from +{fault.Start.TotalSeconds:F0}s"
                + (fault.Duration is { } d ? $" for {d.TotalSeconds:F0}s." : " until stopped."));
        }

        // Scenario timings count from here, when the fleet starts sending.
        var clockStart = DateTimeOffset.UtcNow;
        var loop = settings.Loop with { Jitter = FleetJitter, LogSuccess = false, ReportStateOnlyOnChange = true };
        var devices = plan.Select(spec => new SimulatedDevice(
            spec.DeviceId,
            spec.Name,
            clients[spec.DeviceId].DeviceClient,
            clients[spec.DeviceId].BundleClient,
            loop,
            // Spread device start times over one heartbeat interval.
            settings.Loop.HeartbeatInterval * spec.StartOffsetFraction,
            spec.Seed,
            spec.Environment,
            new DeviceRuntime(nats, faults.GetValueOrDefault(spec.DeviceId), clockStart)));

        await Task.WhenAll(devices.Select(d => d.RunAsync(cancellationToken)));
    }

    // Operator calls only: registering, approving and grouping devices.
    private static HttpClient CreateOperatorClient(SimulatorSettings settings)
    {
        var apiKey = ResolveRequiredOption(settings.ApiKey, "SIM_API_KEY", "--api-key");
        var client = new HttpClient { BaseAddress = new Uri(settings.DeviceManagerUrl.TrimEnd('/')) };
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static (HttpClient DeviceClient, HttpClient? BundleClient) CreateDeviceKeyClients(
        SimulatorSettings settings,
        HttpMessageHandler handler,
        string deviceKey)
    {
        HttpClient Create(string baseUrl)
        {
            var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
            client.DefaultRequestHeaders.Add("X-Api-Key", deviceKey);
            return client;
        }

        return (
            Create(settings.DeviceManagerUrl),
            string.IsNullOrWhiteSpace(settings.BundleOrchestratorUrl) ? null : Create(settings.BundleOrchestratorUrl));
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
                _deviceCredentials = JsonSerializer.Deserialize<DeviceCredentials>(json, SimulatorJson.Options);

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

            var statusData = await statusResponse.Content.ReadFromJsonAsync<GetRegistrationStatusResponse>(SimulatorJson.Options);

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

        var statusData = await response.Content.ReadFromJsonAsync<GetRegistrationStatusResponse>(SimulatorJson.Options);
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

    private static async Task RequestCertificateAsync(
        string deviceManagerUrl,
        Guid deviceId,
        string credentialsPath)
    {
        if (_deviceCredentials == null)
        {
            throw new InvalidOperationException("Device credentials not initialized.");
        }

        // If we already have a valid certificate, skip
        if (!string.IsNullOrEmpty(_deviceCredentials.ClientCertificatePath)
            && File.Exists(_deviceCredentials.ClientCertificatePath)
            && _deviceCredentials.CertificateExpiresAt > DateTimeOffset.UtcNow)
        {
            Console.WriteLine($"Using existing certificate (serial: {_deviceCredentials.CertificateSerialNumber}, expires: {_deviceCredentials.CertificateExpiresAt:O}).");
            return;
        }

        Console.WriteLine("Requesting mTLS certificate...");

        using var client = new HttpClient { BaseAddress = new Uri(deviceManagerUrl.TrimEnd('/')) };
        if (!string.IsNullOrEmpty(_deviceCredentials.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", _deviceCredentials.ApiKey);
        }

        var response = await client.PostAsync($"/api/certificates/{deviceId}/issue", null);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Certificate issuance failed: {response.StatusCode} {content}");
        }

        var certResponse = await response.Content.ReadFromJsonAsync<IssueCertificateSimResponse>(SimulatorJson.Options);
        if (certResponse == null)
        {
            throw new InvalidOperationException("Certificate issuance returned empty response.");
        }

        // Save certificate files
        var certsDir = Path.Combine(Path.GetDirectoryName(credentialsPath) ?? ".", $"sim-credentials-{deviceId}", "certs");
        Directory.CreateDirectory(certsDir);

        var certPath = Path.Combine(certsDir, "client-cert.pem");
        var keyPath = Path.Combine(certsDir, "client-key.pem");
        var caPath = Path.Combine(certsDir, "ca-cert.pem");

        // Create files with restrictive permissions BEFORE writing sensitive content (Unix only)
        if (!OperatingSystem.IsWindows())
        {
            await File.WriteAllTextAsync(keyPath, string.Empty);
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await File.WriteAllTextAsync(certPath, string.Empty);
            File.SetUnixFileMode(certPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        await File.WriteAllTextAsync(certPath, certResponse.CertificatePem);
        await File.WriteAllTextAsync(keyPath, certResponse.PrivateKeyPem);
        await File.WriteAllTextAsync(caPath, certResponse.CaCertificatePem);

        // Update credentials
        _deviceCredentials.ClientCertificatePath = certPath;
        _deviceCredentials.ClientPrivateKeyPath = keyPath;
        _deviceCredentials.CaCertificatePath = caPath;
        _deviceCredentials.CertificateSerialNumber = certResponse.SerialNumber;
        _deviceCredentials.CertificateExpiresAt = certResponse.ExpiresAt;

        await SaveCredentialsAsync(credentialsPath);

        Console.WriteLine($"Certificate issued successfully:");
        Console.WriteLine($"  Serial:  {certResponse.SerialNumber}");
        Console.WriteLine($"  Expires: {certResponse.ExpiresAt:O}");
        Console.WriteLine($"  Cert:    {certPath}");
        Console.WriteLine($"  Key:     {keyPath}");
        Console.WriteLine($"  CA:      {caPath}");
    }

    private static HttpClient CreateClientWithMtls(string baseUrl)
    {
        if (_deviceCredentials == null
            || string.IsNullOrEmpty(_deviceCredentials.ClientCertificatePath)
            || string.IsNullOrEmpty(_deviceCredentials.ClientPrivateKeyPath))
        {
            throw new InvalidOperationException("Certificate not available. Run with --use-mtls after device approval.");
        }

        var certPem = File.ReadAllText(_deviceCredentials.ClientCertificatePath);
        var keyPem = File.ReadAllText(_deviceCredentials.ClientPrivateKeyPath);
        var clientCert = X509Certificate2.CreateFromPem(certPem, keyPem);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCert);

        // If CA cert exists, add custom validation
        if (!string.IsNullOrEmpty(_deviceCredentials.CaCertificatePath)
            && File.Exists(_deviceCredentials.CaCertificatePath))
        {
            var caCertPem = File.ReadAllText(_deviceCredentials.CaCertificatePath);
            var caCert = X509Certificate2.CreateFromPem(caCertPem);

            handler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None)
                    return true;

                if (chain != null)
                {
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    return chain.Build(new X509Certificate2(cert!));
                }

                return false;
            };
        }

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/'))
        };

        // Also set API key header as fallback
        if (!string.IsNullOrEmpty(_deviceCredentials.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", _deviceCredentials.ApiKey);
        }

        Console.WriteLine($"HttpClient configured with mTLS certificate (serial: {_deviceCredentials.CertificateSerialNumber}).");
        return client;
    }

    private record IssueCertificateSimResponse(
        Guid DeviceId,
        string CertificatePem,
        string PrivateKeyPem,
        string CaCertificatePem,
        string SerialNumber,
        string Fingerprint,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt);

    private record DeviceCredentials
    {
        public Guid DeviceId { get; set; }
        public Guid TenantId { get; set; }
        public string? ApiKey { get; set; }
        public DateTimeOffset? ApiKeyExpiresAt { get; set; }
        public string RegistrationStatus { get; set; } = "Pending";
        public string? ClientCertificatePath { get; set; }
        public string? ClientPrivateKeyPath { get; set; }
        public string? CaCertificatePath { get; set; }
        public string? CertificateSerialNumber { get; set; }
        public DateTimeOffset? CertificateExpiresAt { get; set; }
    }

    private record GetRegistrationStatusResponse(
        string Status,
        string? ApiKey = null,
        DateTimeOffset? ApiKeyExpiresAt = null);
}
