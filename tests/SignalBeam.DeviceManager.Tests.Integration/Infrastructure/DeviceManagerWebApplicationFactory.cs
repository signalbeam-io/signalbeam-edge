using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.DeviceManager.Host;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Shared.Infrastructure.Authentication;
using Testcontainers.PostgreSql;

namespace SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

/// <summary>
/// WebApplicationFactory for testing DeviceManager HTTP endpoints.
/// </summary>
public class DeviceManagerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly Guid _defaultTenantId = Guid.NewGuid();

    public Guid DefaultTenantId => _defaultTenantId;

    public DeviceManagerWebApplicationFactory()
    {
        _postgresContainer = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
            .WithDatabase("signalbeam_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Deterministic rate limiting for tests: no queue, so requests beyond the permit limit are
        // rejected immediately instead of waiting a full window (which would make the test hang).
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "100",
                ["RateLimiting:WindowSeconds"] = "60",
                ["RateLimiting:QueueLimit"] = "0"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll<DbContextOptions<DeviceDbContext>>();
            services.RemoveAll<DeviceDbContext>();

            // Add test database context
            services.AddDbContext<DeviceDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            // Replace API key validator with test implementation
            services.RemoveAll<IApiKeyValidator>();
            services.AddSingleton<IApiKeyValidator>(new TestApiKeyValidator(_defaultTenantId));

            // Replace IdentityManager-backed quota validator with a test stub so device
            // registration doesn't make a real HTTP call to a non-running IdentityManager.
            services.RemoveAll<IDeviceQuotaValidator>();
            services.AddSingleton<IDeviceQuotaValidator>(new TestDeviceQuotaValidator());

            // Functional tests register several devices from the same loopback IP within the window;
            // keep the per-IP registration limit effectively disabled here. The dedicated
            // registration rate-limit test overrides this with a low value. (Configured via DI
            // because ConfigureAppConfiguration does not override startup-read config in the WAF.)
            services.Configure<RegistrationRateLimitOptions>(o => o.PermitLimit = int.MaxValue);
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Ensure clean database state by dropping all tables and reapplying migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();

        // Ensure clean database state
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates an HTTP client with API key authentication headers.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string apiKey = "test-api-key")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }
}
