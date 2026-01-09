using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to upgrade a tenant's subscription tier.
/// </summary>
public record UpgradeSubscriptionCommand(
    Guid TenantId,
    SubscriptionTier NewTier,
    Guid UpgradedByUserId);

/// <summary>
/// Response after successful subscription upgrade.
/// </summary>
public record UpgradeSubscriptionResponse(
    Guid TenantId,
    SubscriptionTier OldTier,
    SubscriptionTier NewTier,
    int MaxDevices,
    int DataRetentionDays,
    DateTimeOffset UpgradedAt);

/// <summary>
/// Handler for upgrading tenant subscription.
/// Updates tenant quotas and subscription tier atomically.
/// </summary>
public class UpgradeSubscriptionHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;

    public UpgradeSubscriptionHandler(
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<UpgradeSubscriptionResponse>> Handle(
        UpgradeSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify user has admin role
        var user = await _userRepository.GetByIdAsync(new UserId(command.UpgradedByUserId), cancellationToken);
        if (user == null)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.NotFound("USER_NOT_FOUND", "User not found."));
        }

        if (user.Role != UserRole.Admin)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.Forbidden("INSUFFICIENT_PERMISSIONS", "Only administrators can upgrade subscriptions."));
        }

        var tenantId = new TenantId(command.TenantId);

        // 2. Verify user belongs to the tenant
        if (user.TenantId != tenantId)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.Forbidden("CROSS_TENANT_ACCESS", "Cannot upgrade subscription for another tenant."));
        }

        // 3. Get tenant
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.NotFound("TENANT_NOT_FOUND", "Tenant not found."));
        }

        // 4. Validate upgrade direction (can't downgrade in this handler)
        if (command.NewTier < tenant.SubscriptionTier)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.Validation("INVALID_TIER_CHANGE", "Cannot downgrade subscription. Please contact support."));
        }

        if (command.NewTier == tenant.SubscriptionTier)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.Validation("SAME_TIER", "Tenant is already on this subscription tier."));
        }

        // 5. Get active subscription
        var subscription = await _subscriptionRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (subscription == null)
        {
            return Result.Failure<UpgradeSubscriptionResponse>(
                Error.NotFound("SUBSCRIPTION_NOT_FOUND", "No active subscription found for tenant."));
        }

        // 6. Store old tier for response
        var oldTier = tenant.SubscriptionTier;
        var upgradedAt = DateTimeOffset.UtcNow;

        // 7. Upgrade tenant (updates tier, max devices, data retention)
        tenant.UpgradeSubscription(command.NewTier, upgradedAt);

        // 8. Update subscription tier
        subscription.ChangeTier(command.NewTier, upgradedAt);

        // 9. Persist changes atomically
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new UpgradeSubscriptionResponse(
            tenant.Id.Value,
            oldTier,
            command.NewTier,
            tenant.MaxDevices,
            tenant.DataRetentionDays,
            upgradedAt));
    }
}
