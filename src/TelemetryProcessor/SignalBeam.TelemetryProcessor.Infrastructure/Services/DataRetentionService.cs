using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

/// <summary>
/// Service for enforcing data retention policies based on tenant subscription tiers.
/// Deletes metrics and heartbeats older than the retention policy for each tenant.
/// </summary>
public class DataRetentionService : IDataRetentionService
{
    private readonly ITenantRetentionClient _tenantClient;
    private readonly IDeviceClient _deviceClient;
    private readonly IDeviceMetricsRepository _metricsRepository;
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(
        ITenantRetentionClient tenantClient,
        IDeviceClient deviceClient,
        IDeviceMetricsRepository metricsRepository,
        IDeviceHeartbeatRepository heartbeatRepository,
        ILogger<DataRetentionService> logger)
    {
        _tenantClient = tenantClient;
        _deviceClient = deviceClient;
        _metricsRepository = metricsRepository;
        _heartbeatRepository = heartbeatRepository;
        _logger = logger;
    }

    /// <summary>
    /// Deletes metrics and heartbeats older than the retention policy for all tenants.
    /// </summary>
    public async Task<Result<DataRetentionResult>> EnforceDataRetentionAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting data retention enforcement");

        try
        {
            // Get all tenants with their retention policies
            var tenantsResult = await _tenantClient.GetAllTenantsWithRetentionAsync(cancellationToken);
            if (tenantsResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to fetch tenant retention policies: {Error}",
                    tenantsResult.Error!.Message);
                return Result.Failure<DataRetentionResult>(tenantsResult.Error);
            }

            var tenants = tenantsResult.Value!;
            if (tenants.Count == 0)
            {
                _logger.LogInformation("No tenants found for data retention enforcement");
                stopwatch.Stop();
                return Result.Success(new DataRetentionResult(0, 0, 0, stopwatch.Elapsed));
            }

            _logger.LogInformation("Processing data retention for {TenantCount} tenants", tenants.Count);

            var totalMetricsDeleted = 0;
            var totalHeartbeatsDeleted = 0;
            var tenantsProcessed = 0;

            foreach (var tenant in tenants)
            {
                try
                {
                    _logger.LogInformation(
                        "Processing tenant {TenantId} ({TenantName}) with {RetentionDays} days retention",
                        tenant.TenantId,
                        tenant.TenantName,
                        tenant.DataRetentionDays);

                    var tenantId = new TenantId(tenant.TenantId);
                    var cutoffDate = DateTimeOffset.UtcNow.AddDays(-tenant.DataRetentionDays);

                    // Get all devices for this tenant
                    var devicesResult = await _deviceClient.GetDeviceIdsByTenantAsync(tenantId, cancellationToken);
                    if (devicesResult.IsFailure)
                    {
                        _logger.LogError(
                            "Failed to fetch devices for tenant {TenantId}: {Error}",
                            tenant.TenantId,
                            devicesResult.Error!.Message);
                        continue; // Skip this tenant and move to the next
                    }

                    var deviceIds = devicesResult.Value!;
                    if (deviceIds.Count == 0)
                    {
                        _logger.LogDebug("No devices found for tenant {TenantId}, skipping", tenant.TenantId);
                        tenantsProcessed++;
                        continue;
                    }

                    // Delete old metrics for all devices in this tenant
                    var metricsDeleted = await _metricsRepository.DeleteOldMetricsAsync(
                        deviceIds,
                        cutoffDate,
                        cancellationToken);

                    _logger.LogInformation(
                        "Deleted {MetricsCount} metrics for tenant {TenantId} ({DeviceCount} devices) older than {CutoffDate}",
                        metricsDeleted,
                        tenant.TenantId,
                        deviceIds.Count,
                        cutoffDate);

                    // Delete old heartbeats for all devices in this tenant
                    var heartbeatsDeleted = await _heartbeatRepository.DeleteOldHeartbeatsAsync(
                        deviceIds,
                        cutoffDate,
                        cancellationToken);

                    _logger.LogInformation(
                        "Deleted {HeartbeatsCount} heartbeats for tenant {TenantId} ({DeviceCount} devices) older than {CutoffDate}",
                        heartbeatsDeleted,
                        tenant.TenantId,
                        deviceIds.Count,
                        cutoffDate);

                    totalMetricsDeleted += metricsDeleted;
                    totalHeartbeatsDeleted += heartbeatsDeleted;
                    tenantsProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing data retention for tenant {TenantId} ({TenantName})",
                        tenant.TenantId,
                        tenant.TenantName);

                    // Continue processing other tenants
                }
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Data retention enforcement completed. Processed {TenantsProcessed} tenants, " +
                "deleted {MetricsDeleted} metrics and {HeartbeatsDeleted} heartbeats in {ElapsedMs}ms",
                tenantsProcessed,
                totalMetricsDeleted,
                totalHeartbeatsDeleted,
                stopwatch.ElapsedMilliseconds);

            return Result.Success(new DataRetentionResult(
                tenantsProcessed,
                totalMetricsDeleted,
                totalHeartbeatsDeleted,
                stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during data retention enforcement");
            return Result.Failure<DataRetentionResult>(
                Error.Unexpected(
                    "DATA_RETENTION_FAILED",
                    $"Data retention enforcement failed: {ex.Message}"));
        }
    }
}
