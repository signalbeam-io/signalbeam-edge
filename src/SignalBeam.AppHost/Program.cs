var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL password parameter
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// Infrastructure
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var signalbeamDb = postgres.AddDatabase("signalbeam");

// Zitadel database (separate from SignalBeam DB)
var zitadelDb = postgres.AddDatabase("zitadel");

var valkey = builder.AddRedis("valkey")
    .WithLifetime(ContainerLifetime.Persistent);

// NATS messaging with JetStream
var nats = builder.AddContainer("nats", "nats", "latest")
    .WithArgs("--jetstream")
    .WithHttpEndpoint(8222, targetPort: 8222, name: "management")
    .WithEndpoint(4222, targetPort: 4222, name: "nats")
    .WithLifetime(ContainerLifetime.Persistent);

// Zitadel - OIDC Authentication and Identity Management
var zitadel = builder.AddContainer("zitadel", "ghcr.io/zitadel/zitadel", "v2.66.3")
    .WithArgs("start-from-init", "--masterkey", "MasterkeyNeedsToHave32Characters", "--tlsMode", "disabled")
    .WithHttpEndpoint(port: 9080, targetPort: 8080, name: "zitadel")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_HOST", postgres.Resource.Name)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_PORT", "5432")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_DATABASE", "zitadel")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_USERNAME", "postgres")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_SSL_MODE", "disable")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME", "postgres")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_SSL_MODE", "disable")
    .WithEnvironment("ZITADEL_EXTERNALSECURE", "false")
    .WithEnvironment("ZITADEL_EXTERNALDOMAIN", "localhost:8080")
    .WithEnvironment("ZITADEL_EXTERNALPORT", "8080")
    .WaitFor(postgres)
    .WithLifetime(ContainerLifetime.Persistent);

// Azurite - Azure Storage Emulator for local development
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

// Microservices
var deviceManager = builder.AddProject<Projects.SignalBeam_DeviceManager_Host>("device-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false");

var bundleOrchestrator = builder.AddProject<Projects.SignalBeam_BundleOrchestrator_Host>("bundle-orchestrator")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithReference(blobs)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false");

var telemetryProcessor = builder.AddProject<Projects.SignalBeam_TelemetryProcessor_Host>("telemetry-processor")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false");

var identityManager = builder.AddProject<Projects.SignalBeam_IdentityManager_Host>("identity-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false");

// API Gateway - Single entry point for all services
// Note: Zitadel routing is handled by YARP config, not WithReference
var apiGateway = builder.AddProject<Projects.SignalBeam_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 8080, name: "gateway")
    .WithReference(deviceManager)
    .WithReference(bundleOrchestrator)
    .WithReference(telemetryProcessor)
    .WithReference(identityManager)
    .WithEnvironment("ReverseProxy__Clusters__zitadel__Destinations__destination1__Address", zitadel.GetEndpoint("zitadel"));

// Edge Agent Simulator - use hardcoded gateway URL for simplicity
var edgeAgentSimulator = builder.AddProject<Projects.SignalBeam_EdgeAgent_Simulator>("edge-agent-simulator")
    .WithEnvironment("SIM_DEVICE_MANAGER_URL", "http://localhost:8080")
    .WithEnvironment("SIM_BUNDLE_ORCHESTRATOR_URL", "http://localhost:8080")
    .WithEnvironment("SIM_API_KEY", "dev-api-key-1")
    .WithEnvironment("SIM_TENANT_ID", "00000000-0000-0000-0000-000000000001");

builder.Build().Run();
