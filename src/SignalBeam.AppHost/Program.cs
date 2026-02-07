var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL password parameter
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// Infrastructure
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
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

// Microservices - all wait for Zitadel setup and read dynamic config
var deviceManager = builder.AddProject<Projects.SignalBeam_DeviceManager_Host>("device-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

var bundleOrchestrator = builder.AddProject<Projects.SignalBeam_BundleOrchestrator_Host>("bundle-orchestrator")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithReference(blobs)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", "http://localhost:8080")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false")
    .WithEnvironment("ZITADEL_CONFIG_PATH", zitadelConfigPath)
    .WaitFor(zitadelSetup);

var telemetryProcessor = builder.AddProject<Projects.SignalBeam_TelemetryProcessor_Host>("telemetry-processor")
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
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithEnvironment("VITE_API_URL", apiGateway.GetEndpoint("gateway"))
    .WithEnvironment("BROWSER", "none")
    .WithExternalHttpEndpoints();

// Edge Agent Simulator - use hardcoded gateway URL for simplicity
var edgeAgentSimulator = builder.AddProject<Projects.SignalBeam_EdgeAgent_Simulator>("edge-agent-simulator")
    .WithEnvironment("SIM_DEVICE_MANAGER_URL", "http://localhost:8080")
    .WithEnvironment("SIM_BUNDLE_ORCHESTRATOR_URL", "http://localhost:8080")
    .WithEnvironment("SIM_API_KEY", "dev-api-key-1")
    .WithEnvironment("SIM_TENANT_ID", "00000000-0000-0000-0000-000000000001");

builder.Build().Run();
