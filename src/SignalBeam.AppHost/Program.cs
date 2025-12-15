var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var signalbeamDb = postgres.AddDatabase("signalbeam");

var valkey = builder.AddRedis("valkey")
    .WithLifetime(ContainerLifetime.Persistent);

// TODO: Add NATS container when microservices are ready
// var nats = builder.AddContainer("nats", "nats", "latest")
//     .WithArgs("--jetstream")
//     .WithHttpEndpoint(8222, targetPort: 8222, name: "management")
//     .WithEndpoint(4222, targetPort: 4222, name: "nats")
//     .WithLifetime(ContainerLifetime.Persistent);

// TODO: Add MinIO container when microservices are ready
// var minio = builder.AddContainer("minio", "minio/minio", "latest")
//     .WithArgs("server", "/data", "--console-address", ":9001")
//     .WithHttpEndpoint(9000, targetPort: 9000, name: "api")
//     .WithHttpEndpoint(9001, targetPort: 9001, name: "console")
//     .WithLifetime(ContainerLifetime.Persistent);

// Microservices - TODO: Add when they are created
// var deviceManager = builder.AddProject<Projects.DeviceManager_Host>("device-manager")
//     .WithReference(signalbeamDb)
//     .WithReference(valkey);

// var bundleOrchestrator = builder.AddProject<Projects.BundleOrchestrator_Host>("bundle-orchestrator")
//     .WithReference(signalbeamDb)
//     .WithReference(valkey);

// var telemetryProcessor = builder.AddProject<Projects.TelemetryProcessor_Host>("telemetry-processor")
//     .WithReference(signalbeamDb)
//     .WithReference(valkey);

builder.Build().Run();
