using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("signalbeam_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Apply migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
        await context.Database.MigrateAsync();
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
