using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Application.Services;

namespace SignalBeam.DeviceManager.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically updates dynamic group memberships.
/// Evaluates tag queries against all devices and syncs group memberships.
/// </summary>
public class DynamicGroupUpdateService : BackgroundService
{
    private readonly ILogger<DynamicGroupUpdateService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DynamicGroupUpdateOptions _options;

    public DynamicGroupUpdateService(
        ILogger<DynamicGroupUpdateService> logger,
        IServiceProvider serviceProvider,
        IOptions<DynamicGroupUpdateOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Dynamic Group Update Service is disabled");
            return;
        }

        _logger.LogInformation(
            "Dynamic Group Update Service started. Update interval: {Interval} minutes",
            _options.UpdateIntervalMinutes);

        // Initial delay to allow application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAllDynamicGroupsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating dynamic group memberships");
            }

            // Wait for the next update interval
            await Task.Delay(TimeSpan.FromMinutes(_options.UpdateIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Dynamic Group Update Service stopped");
    }

    private Task UpdateAllDynamicGroupsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting dynamic group membership update cycle");

        using var scope = _serviceProvider.CreateScope();
        var membershipManager = scope.ServiceProvider.GetRequiredService<IDynamicGroupMembershipManager>();

        // TODO: For multi-tenant support, iterate through all tenants
        // For now, we log a warning that this needs to be implemented
        _logger.LogWarning(
            "UpdateAllDynamicGroupsAsync requires tenant-specific iteration. " +
            "Consider calling UpdateDynamicGroupsForTenantAsync per tenant instead.");

        // In a real implementation, you would:
        // 1. Get all tenants from a tenant repository
        // 2. For each tenant, call membershipManager.UpdateDynamicGroupsForTenantAsync(tenantId)
        // 3. Track statistics and log results

        _logger.LogInformation("Completed dynamic group membership update cycle");

        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for dynamic group update service.
/// </summary>
public class DynamicGroupUpdateOptions
{
    public const string SectionName = "DynamicGroupUpdate";

    /// <summary>
    /// How often to update dynamic groups (in minutes). Default: 5 minutes.
    /// </summary>
    public double UpdateIntervalMinutes { get; set; } = 5.0;

    /// <summary>
    /// Enable or disable the background service. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
