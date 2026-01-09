using Microsoft.Extensions.Logging;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.IdentityManager.Application.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Infrastructure.Services;

/// <summary>
/// Implementation of quota enforcement service.
/// Checks device quotas and enforces data retention policies.
/// </summary>
public class QuotaEnforcementService : IQuotaEnforcementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<QuotaEnforcementService> _logger;

    public QuotaEnforcementService(
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<QuotaEnforcementService> logger)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task<Result> CheckDeviceQuotaAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // Get tenant to check max devices
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found during quota check", tenantId);
            return Result.Failure(
                Error.NotFound("TENANT_NOT_FOUND", "Tenant not found."));
        }

        // Get active subscription to check current device count
        var subscription = await _subscriptionRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (subscription == null)
        {
            _logger.LogWarning("No active subscription found for tenant {TenantId}", tenantId);
            return Result.Failure(
                Error.NotFound("SUBSCRIPTION_NOT_FOUND", "No active subscription found for tenant."));
        }

        // Check if tenant can add another device
        if (!tenant.CanAddDevice(subscription.DeviceCount))
        {
            _logger.LogInformation(
                "Device quota exceeded for tenant {TenantId}. Current: {CurrentCount}, Max: {MaxDevices}",
                tenantId, subscription.DeviceCount, tenant.MaxDevices);

            return Result.Failure(
                Error.Validation(
                    "DEVICE_QUOTA_EXCEEDED",
                    $"Device quota exceeded. Your {tenant.SubscriptionTier} plan allows up to {tenant.MaxDevices} devices. " +
                    $"Current count: {subscription.DeviceCount}. Please upgrade your subscription to add more devices."));
        }

        _logger.LogDebug(
            "Quota check passed for tenant {TenantId}. Current: {CurrentCount}/{MaxDevices}",
            tenantId, subscription.DeviceCount, tenant.MaxDevices);

        return Result.Success();
    }

    public async Task<int> GetCurrentDeviceCountAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (subscription == null)
        {
            _logger.LogWarning("No active subscription found for tenant {TenantId} when getting device count", tenantId);
            return 0;
        }

        return subscription.DeviceCount;
    }

    public async Task<Result> EnforceDataRetentionAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found during data retention enforcement", tenantId);
            return Result.Failure(
                Error.NotFound("TENANT_NOT_FOUND", "Tenant not found."));
        }

        // Calculate cutoff date based on tenant's retention policy
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-tenant.DataRetentionDays);

        _logger.LogInformation(
            "Data retention enforcement for tenant {TenantId}: {RetentionDays} days, cutoff: {CutoffDate}",
            tenantId, tenant.DataRetentionDays, cutoffDate);

        // NOTE: Actual deletion of old metrics/telemetry data will be implemented
        // in TelemetryProcessor background worker (Issue #7)
        // This method serves as a validation that retention policy can be enforced

        return Result.Success();
    }
}
