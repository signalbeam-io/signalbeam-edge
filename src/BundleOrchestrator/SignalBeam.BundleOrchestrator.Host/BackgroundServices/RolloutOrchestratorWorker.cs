using Microsoft.Extensions.Options;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Host.BackgroundServices;

/// <summary>
/// Background service that orchestrates automatic phased rollout progression.
/// Runs periodically to check all active rollouts and advance phases when conditions are met.
/// </summary>
public class RolloutOrchestratorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RolloutOrchestratorWorker> _logger;
    private readonly RolloutOrchestratorOptions _options;

    public RolloutOrchestratorWorker(
        IServiceProvider serviceProvider,
        ILogger<RolloutOrchestratorWorker> logger,
        IOptions<RolloutOrchestratorOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RolloutOrchestratorWorker started with interval {Interval}",
            _options.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRolloutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing rollouts");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.CheckIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInformation("RolloutOrchestratorWorker stopping");
    }

    private async Task ProcessRolloutsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var orchestrationService = scope.ServiceProvider.GetRequiredService<RolloutOrchestrationService>();
        var rolloutRepository = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        // Get distinct tenant IDs from all active rollouts
        var tenantIds = await GetTenantsWithActiveRolloutsAsync(
            rolloutRepository,
            cancellationToken);

        _logger.LogInformation(
            "Processing rollouts for {TenantCount} tenants",
            tenantIds.Count);

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await orchestrationService.ProcessActiveRolloutsAsync(tenantId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing rollouts for tenant {TenantId}", tenantId);
            }
        }
    }

    /// <summary>
    /// Get list of tenant IDs that have active rollouts.
    /// </summary>
    private Task<IReadOnlyList<TenantId>> GetTenantsWithActiveRolloutsAsync(
        IRolloutRepository rolloutRepository,
        CancellationToken cancellationToken)
    {
        // Note: This is a temporary implementation for MVP
        // In production, add a dedicated method to IRolloutRepository: GetActiveRolloutTenantIdsAsync()
        // For now, we'll need to iterate through tenants or use a configured list

        // TODO: Implement GetActiveRolloutTenantIdsAsync in IRolloutRepository
        // For MVP, return empty list (manual rollout advancement via API)
        // or configure tenant IDs in appsettings.json

        _logger.LogDebug(
            "Automatic rollout processing requires GetActiveRolloutTenantIdsAsync implementation. " +
            "Currently using manual rollout advancement via API.");

        return Task.FromResult<IReadOnlyList<TenantId>>(Array.Empty<TenantId>());
    }
}

/// <summary>
/// Configuration options for the rollout orchestrator worker.
/// </summary>
public class RolloutOrchestratorOptions
{
    public const string SectionName = "RolloutOrchestrator";

    /// <summary>
    /// How often to check for rollout updates (in seconds).
    /// Default: 30 seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum time a phase must be healthy before auto-advancing (in minutes).
    /// This is a global default that can be overridden per-phase.
    /// Default: 5 minutes.
    /// </summary>
    public int DefaultMinHealthyDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of concurrent rollout processings per check cycle.
    /// Default: 10.
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 10;
}
