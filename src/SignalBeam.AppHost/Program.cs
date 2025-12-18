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
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

var bundleOrchestrator = builder.AddProject<Projects.SignalBeam_BundleOrchestrator_Host>("bundle-orchestrator")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithReference(blobs)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

var telemetryProcessor = builder.AddProject<Projects.SignalBeam_TelemetryProcessor_Host>("telemetry-processor")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"));

builder.Build().Run();
