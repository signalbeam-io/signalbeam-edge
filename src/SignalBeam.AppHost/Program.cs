var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL password parameter
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// Infrastructure
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    // TimescaleDB (Apache-only "-oss" build, matching Azure's TimescaleDB) on Postgres 17:
    // - TelemetryProcessor's migrations need the timescaledb extension; the plain postgres image lacks it.
    // - Zitadel v2.66.3 cannot initialise on Postgres 18+ (migration 34_add_cache_schema fails with
    //   "partitioned tables cannot be unlogged"). Stay on 17 until Zitadel is upgraded to v4 (#483).
    .WithImage("timescale/timescaledb", "2.32.1-pg17-oss")
    .WithImageRegistry("docker.io")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var signalbeamDb = postgres.AddDatabase("signalbeam");

// Zitadel database (separate from SignalBeam DB)
// Note: Database resource name must be different from container name
var zitadelDb = postgres.AddDatabase("zitadel-db");

var valkey = builder.AddRedis("valkey")
    .WithLifetime(ContainerLifetime.Persistent);

// NATS messaging with JetStream
var nats = builder.AddContainer("nats", "nats", "latest")
    .WithArgs("--jetstream")
    .WithHttpEndpoint(8222, targetPort: 8222, name: "management")
    .WithEndpoint(4222, targetPort: 4222, name: "nats")
    .WithLifetime(ContainerLifetime.Persistent);

// Shared directory for Zitadel PAT token (bind mounted into container)
// Use a project-local .zitadel directory so PAT survives reboots (added to .gitignore)
var appHostDir = AppContext.BaseDirectory;
var repoRoot = appHostDir;
while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot, ".git")))
{
    repoRoot = Directory.GetParent(repoRoot)?.FullName;
}

repoRoot ??= appHostDir;

// OpenTelemetry Collector between the services and the dashboard. Besides forwarding
// everything to the dashboard it writes traces as JSON Lines to .tracebeam/otlp/, the
// machine-readable record of system behaviour used by tracebeam and coding agents (#472).
var traceExportDir = Path.Combine(repoRoot, ".tracebeam", "otlp");
Directory.CreateDirectory(traceExportDir);
if (!OperatingSystem.IsWindows())
{
    // The collector image runs as a non-root user (UID 10001). On Linux hosts the bind-mounted
    // directory keeps host ownership, so make this git-ignored, local-only folder writable for it.
    File.SetUnixFileMode(traceExportDir,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
}

builder.AddOpenTelemetryCollector("otel-collector", settings =>
    {
        // Pinned Docker Hub image instead of the integration's default ghcr.io ":latest"
        // (reproducible, and avoids pull failures from stale ghcr.io credentials).
        settings.Registry = "docker.io";
        settings.Image = "otel/opentelemetry-collector-contrib";
        settings.CollectorTag = "0.164.0";
    })
    .WithConfig(Path.Combine(builder.AppHostDirectory, "otel-collector.yaml"))
    .WithBindMount(traceExportDir, "/tracebeam/otlp")
    .WithAppForwarding();

var zitadelPatDir = Path.Combine(repoRoot, ".zitadel");
Directory.CreateDirectory(zitadelPatDir);
var zitadelPatPath = Path.Combine(zitadelPatDir, "token");

// Zitadel - OIDC Authentication and Identity Management
var zitadel = builder.AddContainer("zitadel", "ghcr.io/zitadel/zitadel", "v2.66.3")
    .WithArgs("start-from-init", "--masterkey", "MasterkeyNeedsToHave32Characters", "--tlsMode", "disabled")
    .WithHttpEndpoint(port: 9080, targetPort: 8080, name: "zitadel")
    .WithBindMount(zitadelPatDir, "/pat")
    // Database configuration
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_HOST", postgres.Resource.Name)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_PORT", "5432")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_DATABASE", zitadelDb.Resource.DatabaseName)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_USERNAME", "postgres")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_SSL_MODE", "disable")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME", "postgres")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_SSL_MODE", "disable")
    // External domain configuration
    .WithEnvironment("ZITADEL_EXTERNALSECURE", "false")
    .WithEnvironment("ZITADEL_EXTERNALDOMAIN", "localhost")
    .WithEnvironment("ZITADEL_EXTERNALPORT", "8080")
    // Default admin user configuration
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME", "admin")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD", "Password1!")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED", "false")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_ADDRESS", "admin@signalbeam.local")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_VERIFIED", "true")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_FIRSTNAME", "SignalBeam")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_LASTNAME", "Admin")
    // Machine user for automated setup (ORG_OWNER role, PAT written to bind mount)
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_MACHINE_USERNAME", "signalbeam-setup")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_MACHINE_NAME", "SignalBeam Setup Service")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_PAT_EXPIRATIONDATE", "2099-01-01T00:00:00Z")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_PATPATH", "/pat/token")
    .WaitFor(postgres)
    .WithLifetime(ContainerLifetime.Persistent);

// Zitadel Auto-Configuration
// This runs once after Zitadel starts to create the project and application via API
var zitadelConfigPath = Path.Combine(Path.GetTempPath(), "signalbeam-zitadel-config.json");
var zitadelSetup = builder.AddProject<Projects.SignalBeam_ZitadelSetup>("zitadel-setup")
    .WithEnvironment("ZITADEL_URL", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("ZITADEL_PAT_FILE", zitadelPatPath)
    .WithEnvironment("CONFIG_OUTPUT_PATH", zitadelConfigPath)
    .WithEnvironment("REPO_ROOT", repoRoot)
    .WaitFor(zitadel);

// Azurite - Azure Storage Emulator for local development
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

// Shared key for service-to-service endpoints under /api/internal (TelemetryProcessor and
// BundleOrchestrator looking up a device's tenant in DeviceManager). Generated per run: every service
// restarts with the AppHost.
var internalServiceKey = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

// Microservices - all wait for Zitadel setup and read dynamic config
var deviceManager = builder.AddProject<Projects.SignalBeam_DeviceManager_Host>("device-manager")
    .WithEnvironment("InternalServices__ApiKey", internalServiceKey)
    // The simulated fleet (~80 req/min) and the operator traffic share the dev tenant's limit
    // (default 100/min), so raise it locally to keep 429s the exception rather than the norm.
    .WithEnvironment("RateLimiting__PermitLimit", "300")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

var bundleOrchestrator = builder.AddProject<Projects.SignalBeam_BundleOrchestrator_Host>("bundle-orchestrator")
    .WithEnvironment("InternalServices__ApiKey", internalServiceKey)
    .WithEnvironment("DeviceManager__BaseUrl", deviceManager.GetEndpoint("http"))
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithReference(blobs)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

var telemetryProcessor = builder.AddProject<Projects.SignalBeam_TelemetryProcessor_Host>("telemetry-processor")
    .WithEnvironment("InternalServices__ApiKey", internalServiceKey)
    .WithEnvironment("DeviceManager__BaseUrl", deviceManager.GetEndpoint("http"))
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

var identityManager = builder.AddProject<Projects.SignalBeam_IdentityManager_Host>("identity-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

// API Gateway - Single entry point for all services
// Note: Zitadel routing is handled by YARP config, not WithReference
var apiGateway = builder.AddProject<Projects.SignalBeam_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 8080, name: "gateway")
    .WithReference(deviceManager)
    .WithReference(bundleOrchestrator)
    .WithReference(telemetryProcessor)
    .WithReference(identityManager)
    .WithEnvironment("ReverseProxy__Clusters__zitadel__Destinations__destination1__Address", zitadel.GetEndpoint("zitadel"));

// Frontend - React + Vite dev server
var frontend = builder.AddNpmApp("frontend", "../../web", "dev")
    .WithHttpEndpoint(port: 5173, targetPort: 5174, env: "PORT")
    .WithEnvironment("VITE_API_URL", apiGateway.GetEndpoint("gateway"))
    .WithEnvironment("BROWSER", "none")
    .WithExternalHttpEndpoints();

// Edge Agent Simulator - use hardcoded gateway URL for simplicity.
// Runs a fleet with a fixed seed so every run produces the same devices and traffic shape.
// Override with e.g. `dotnet run -- --Simulator:FleetSize=5 --Simulator:Seed=7`.
var simulatorFleetSize = builder.Configuration["Simulator:FleetSize"] ?? "20";
var simulatorSeed = builder.Configuration["Simulator:Seed"] ?? "42";

// Optional failure injection, e.g. `dotnet run -- --Simulator:Scenarios=offline,disk-fill --Simulator:ScenarioDevices=2`
// or `--Simulator:ScenarioFile=/path/to/scenarios.json`. See docs/simulator-scenarios.md.
var simulatorArgs = new List<string> { "--fleet-size", simulatorFleetSize, "--seed", simulatorSeed };
if (builder.Configuration["Simulator:Scenarios"] is { Length: > 0 } scenarioNames)
{
    simulatorArgs.AddRange(["--scenario", scenarioNames]);
    simulatorArgs.AddRange(["--scenario-devices", builder.Configuration["Simulator:ScenarioDevices"] ?? "1"]);
    if (builder.Configuration["Simulator:ScenarioStart"] is { Length: > 0 } scenarioStart)
    {
        simulatorArgs.AddRange(["--scenario-start", scenarioStart]);
    }
}

if (builder.Configuration["Simulator:ScenarioFile"] is { Length: > 0 } scenarioFile)
{
    simulatorArgs.AddRange(["--scenario-file", Path.GetFullPath(scenarioFile)]);
}

var edgeAgentSimulator = builder.AddProject<Projects.SignalBeam_EdgeAgent_Simulator>("edge-agent-simulator")
    .WithArgs([.. simulatorArgs])
    .WithEnvironment("SIM_NATS_URL", nats.GetEndpoint("nats"))
    .WithEnvironment("SIM_DEVICE_MANAGER_URL", "http://localhost:8080")
    .WithEnvironment("SIM_BUNDLE_ORCHESTRATOR_URL", "http://localhost:8080")
    .WithEnvironment("SIM_API_KEY", "dev-api-key-1")
    .WithEnvironment("SIM_TENANT_ID", "00000000-0000-0000-0000-000000000001");

// Operator traffic against the control-plane APIs through the gateway, like the web UI.
// Override with e.g. `dotnet run -- --Operator:Rate=60 --Operator:Seed=7`; `--Operator:Enabled=false` turns it off.
if (!string.Equals(builder.Configuration["Operator:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
{
    builder.AddProject<Projects.SignalBeam_TrafficGenerator>("operator-traffic")
        .WithArgs(
            "--seed", builder.Configuration["Operator:Seed"] ?? simulatorSeed,
            "--rate", builder.Configuration["Operator:Rate"] ?? "30",
            "--start-delay", builder.Configuration["Operator:StartDelay"] ?? "60")
        .WithEnvironment("TRAFFIC_GATEWAY_URL", "http://localhost:8080")
        .WithEnvironment("TRAFFIC_API_KEY", "dev-api-key-1")
        .WithEnvironment("TRAFFIC_TENANT_ID", "00000000-0000-0000-0000-000000000001")
        .WaitFor(apiGateway)
        .WaitFor(edgeAgentSimulator);
}

builder.Build().Run();
