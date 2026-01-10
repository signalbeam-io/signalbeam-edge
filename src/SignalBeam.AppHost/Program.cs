var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var signalbeamDb = postgres.AddDatabase("signalbeam");

var valkey = builder.AddRedis("valkey")
    .WithLifetime(ContainerLifetime.Persistent);

// NATS messaging with JetStream
var nats = builder.AddContainer("nats", "nats", "latest")
    .WithArgs("--jetstream")
    .WithHttpEndpoint(8222, targetPort: 8222, name: "management")
    .WithEndpoint(4222, targetPort: 4222, name: "nats")
    .WithLifetime(ContainerLifetime.Persistent);

// Azurite - Azure Storage Emulator for local development
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

// Microservices
var deviceManager = builder.AddProject<Projects.SignalBeam_DeviceManager_Host>("device-manager")
    .WithReference(signalbeamDb)
    .WaitFor(signalbeamDb)
    .WithReference(valkey)
    .WaitFor(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

var bundleOrchestrator = builder.AddProject<Projects.SignalBeam_BundleOrchestrator_Host>("bundle-orchestrator")
    .WithReference(signalbeamDb)
    .WaitFor(signalbeamDb)
    .WithReference(valkey)
    .WaitFor(valkey)
    .WithReference(blobs)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

var telemetryProcessor = builder.AddProject<Projects.SignalBeam_TelemetryProcessor_Host>("telemetry-processor")
    .WithReference(signalbeamDb)
    .WaitFor(signalbeamDb)
    .WithReference(valkey)
    .WaitFor(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

// API Gateway - Single entry point for all services
var apiGateway = builder.AddProject<Projects.SignalBeam_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 8080, name: "gateway")
    .WithReference(deviceManager)
    .WithReference(bundleOrchestrator)
    .WithReference(telemetryProcessor);

// Edge Agent Simulator - use hardcoded gateway URL for simplicity
var edgeAgentSimulator = builder.AddProject<Projects.SignalBeam_EdgeAgent_Simulator>("edge-agent-simulator")
    .WithEnvironment("SIM_DEVICE_MANAGER_URL", "http://localhost:8080")
    .WithEnvironment("SIM_BUNDLE_ORCHESTRATOR_URL", "http://localhost:8080")
    .WithEnvironment("SIM_API_KEY", "dev-api-key-1")
    .WithEnvironment("SIM_TENANT_ID", "00000000-0000-0000-0000-000000000001");

builder.Build().Run();
