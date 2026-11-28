extern alias BundleOrchestratorHost;
extern alias DeviceManagerHost;
extern alias GatewayHost;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;
using Testcontainers.PostgreSql;
using Yarp.ReverseProxy.Forwarder;
using BundleOrchestratorProgram = BundleOrchestratorHost::Program;
using DeviceManagerProgram = DeviceManagerHost::Program;
using GatewayProgram = GatewayHost::Program;
using RegistrationRateLimitOptions = DeviceManagerHost::SignalBeam.DeviceManager.Host.RegistrationRateLimitOptions;

namespace SignalBeam.EndToEnd.Tests.Integration;

// DeviceManager, BundleOrchestrator and the API gateway, each the real app in memory, wired the way
// they run deployed: the gateway forwards by its own route table, and BundleOrchestrator calls
// DeviceManager's internal endpoints with the service key. Only the transport is in-memory.
public sealed class DeviceFlowFixture : IAsyncLifetime
{
    public const string OperatorKey = "e2e-operator-key";
    private const string InternalServiceKey = "e2e-internal-service-key";
    private const string DeviceManagerHost = "device-manager.e2e";
    private const string BundleOrchestratorHostName = "bundle-orchestrator.e2e";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private DeviceManagerFactory _deviceManager = null!;
    private BundleOrchestratorFactory _bundleOrchestrator = null!;
    private GatewayFactory _gateway = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _deviceManager = new DeviceManagerFactory(ConnectionString("signalbeam_dm"), TenantId);
        _deviceManager.StartServer();
        // DeviceManager skips its startup migration in the Testing environment.
        using (var scope = _deviceManager.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeviceDbContext>().Database.MigrateAsync();
        }

        _bundleOrchestrator = new BundleOrchestratorFactory(ConnectionString("signalbeam_bo"), _deviceManager);
        _bundleOrchestrator.StartServer();

        _gateway = new GatewayFactory(new Dictionary<string, Func<HttpMessageHandler>>
        {
            [DeviceManagerHost] = _deviceManager.Server.CreateHandler,
            [BundleOrchestratorHostName] = _bundleOrchestrator.Server.CreateHandler
        });
        _gateway.StartServer();
    }

    public async Task DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _bundleOrchestrator.DisposeAsync();
        await _deviceManager.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // A handler that sends requests into the gateway, for the agent's own HttpClient pipeline.
    public HttpMessageHandler CreateGatewayHandler() => _gateway.Server.CreateHandler();

    public Uri GatewayAddress => _gateway.Server.BaseAddress;

    public HttpClient CreateGatewayClient(string? apiKey)
    {
        var client = _gateway.CreateClient();
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add(AuthenticationConstants.ApiKeyHeaderName, apiKey);
        }

        return client;
    }

    public async Task WithBundleDbAsync(Func<BundleDbContext, Task> action)
    {
        using var scope = _bundleOrchestrator.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<BundleDbContext>());
    }

    private string ConnectionString(string database) =>
        new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;

    private sealed class DeviceManagerFactory(string connectionString, Guid tenantId) : WebApplicationFactory<DeviceManagerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DeviceDb", connectionString);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
                new Dictionary<string, string?> { [InternalServiceAuthentication.ConfigurationKey] = InternalServiceKey }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DeviceDbContext>>();
                services.RemoveAll<DeviceDbContext>();
                services.AddDbContext<DeviceDbContext>(options => options.UseNpgsql(connectionString));

                services.RemoveAll<IApiKeyValidator>();
                services.AddSingleton<IApiKeyValidator>(new OperatorKeyValidator(tenantId));

                services.RemoveAll<IDeviceQuotaValidator>();
                services.AddSingleton<IDeviceQuotaValidator>(new UnlimitedQuotaValidator());

                services.Configure<RegistrationRateLimitOptions>(o => o.PermitLimit = int.MaxValue);
            });
        }
    }

    private sealed class BundleOrchestratorFactory(string connectionString, DeviceManagerFactory deviceManager)
        : WebApplicationFactory<BundleOrchestratorProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:BundleDb", connectionString);
            builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<BundleDbContext>>();
                services.RemoveAll<BundleDbContext>();
                services.AddDbContext<BundleDbContext>(options => options.UseNpgsql(connectionString));

                // Same clients as production, pointed at the in-memory DeviceManager. The service key is
                // read while the app is built, before test configuration applies, so it's set here.
                services.AddHttpClient<IDeviceCredentialClient, DeviceCredentialClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => deviceManager.Server.CreateHandler())
                    .ConfigureHttpClient(WithServiceKey);
                services.AddHttpClient<IDeviceTenantClient, DeviceTenantClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => deviceManager.Server.CreateHandler())
                    .ConfigureHttpClient(WithServiceKey);
            });
        }

        private static void WithServiceKey(HttpClient client)
        {
            client.DefaultRequestHeaders.Remove(InternalServiceAuthentication.HeaderName);
            client.DefaultRequestHeaders.Add(InternalServiceAuthentication.HeaderName, InternalServiceKey);
        }
    }

    private sealed class GatewayFactory(IReadOnlyDictionary<string, Func<HttpMessageHandler>> backends)
        : WebApplicationFactory<GatewayProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:device-manager:Destinations:destination1:Address"] = $"http://{DeviceManagerHost}",
                ["ReverseProxy:Clusters:bundle-orchestrator:Destinations:destination1:Address"] = $"http://{BundleOrchestratorHostName}"
            }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IForwarderHttpClientFactory>();
                services.AddSingleton<IForwarderHttpClientFactory>(new InMemoryForwarderFactory(backends));
            });
        }
    }

    // Sends each proxied request to the in-memory app whose host name the route resolved to.
    private sealed class InMemoryForwarderFactory(IReadOnlyDictionary<string, Func<HttpMessageHandler>> backends)
        : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) =>
            new(new HostRoutingHandler(backends.ToDictionary(b => b.Key, b => new HttpMessageInvoker(b.Value()))), disposeHandler: true);
    }

    private sealed class HostRoutingHandler(IReadOnlyDictionary<string, HttpMessageInvoker> backends) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            backends.TryGetValue(request.RequestUri!.Host, out var backend)
                ? backend.SendAsync(request, cancellationToken)
                : throw new InvalidOperationException($"No in-memory backend for {request.RequestUri}");
    }

    private sealed class OperatorKeyValidator(Guid tenantId) : IApiKeyValidator
    {
        public Task<Result<ApiKeyValidationResult>> ValidateAsync(string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(apiKey == OperatorKey
                ? Result<ApiKeyValidationResult>.Success(new ApiKeyValidationResult { TenantId = tenantId.ToString() })
                : Result.Failure<ApiKeyValidationResult>(Error.Unauthorized("INVALID_API_KEY", "Unknown key.")));
    }

    private sealed class UnlimitedQuotaValidator : IDeviceQuotaValidator
    {
        public Task<Result> CheckDeviceQuotaAsync(SignalBeam.Domain.ValueObjects.TenantId tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
