using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SignalBeam.BundleOrchestrator.Host.BackgroundServices;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.Shared.Infrastructure.InternalServices;
using Testcontainers.PostgreSql;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Integration;

// BundleOrchestrator over HTTP with a real Postgres. DeviceManager lookups are in-memory fakes.
public sealed class BundleOrchestratorFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    public const string TenantAKey = "tenant-a-key";
    public const string TenantBKey = "tenant-b-key";
    public const string NoTenantKey = "no-tenant-key";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
        .WithDatabase("bundles_http_test")
        .Build();

    public FakeDeviceManager DeviceManager { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // The host is built on first use, after InitializeAsync has started the container.
        builder.UseSetting("ConnectionStrings:BundleDb", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:ApiKeys:0"] = $"{TenantA}:{TenantAKey}:bundles:read,bundles:write",
            ["Authentication:ApiKeys:1"] = $"{TenantB}:{TenantBKey}:bundles:read,bundles:write",
            ["Authentication:ApiKeys:2"] = $"{Guid.Empty}:{NoTenantKey}:bundles:read,bundles:write",
            ["RateLimiting:PermitLimit"] = "10000"
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDeviceTenantClient>();
            services.AddSingleton<IDeviceTenantClient>(DeviceManager);
            services.RemoveAll<IDeviceGroupClient>();
            services.AddSingleton<IDeviceGroupClient>(DeviceManager);

            // Rollouts are advanced by hand in these tests.
            var worker = services.FirstOrDefault(d => d.ImplementationType == typeof(RolloutOrchestratorWorker));
            if (worker is not null)
            {
                services.Remove(worker);
            }
        });
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient Client(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    public BundleDbContext DbContext() => Services.CreateScope().ServiceProvider.GetRequiredService<BundleDbContext>();
}

public sealed class FakeDeviceManager : IDeviceTenantClient, IDeviceGroupClient
{
    private readonly Dictionary<Guid, Guid> _deviceTenants = [];
    private readonly Dictionary<Guid, (Guid TenantId, Guid[] Devices)> _groups = [];

    public Guid AddDevice(Guid tenantId)
    {
        var deviceId = Guid.NewGuid();
        lock (_deviceTenants)
        {
            _deviceTenants[deviceId] = tenantId;
        }

        return deviceId;
    }

    public Guid AddGroup(Guid tenantId, params Guid[] devices)
    {
        var groupId = Guid.NewGuid();
        lock (_groups)
        {
            _groups[groupId] = (tenantId, devices);
        }

        return groupId;
    }

    public Task<DeviceTenantLookup> LookupAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        lock (_deviceTenants)
        {
            return Task.FromResult(_deviceTenants.TryGetValue(deviceId, out var tenant)
                ? DeviceTenantLookup.Found(tenant)
                : DeviceTenantLookup.NotFound);
        }
    }

    public Task<DeviceGroupLookup> GetAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken = default)
    {
        lock (_groups)
        {
            return Task.FromResult(_groups.TryGetValue(groupId, out var group) && group.TenantId == tenantId
                ? DeviceGroupLookup.Found(new DeviceGroupMembers(groupId, "group", group.Devices))
                : DeviceGroupLookup.NotFound);
        }
    }
}
