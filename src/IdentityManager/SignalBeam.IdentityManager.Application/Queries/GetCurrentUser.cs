using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Queries;

/// <summary>
/// Query to get current authenticated user's details including tenant and subscription info.
/// </summary>
public record GetCurrentUserQuery(string ZitadelUserId);

/// <summary>
/// Complete user information including tenant context and subscription details.
/// </summary>
public record UserDto(
    Guid UserId,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Email,
    string Name,
    UserRole Role,
    UserStatus Status,
    SubscriptionTier SubscriptionTier,
    int MaxDevices,
    int CurrentDeviceCount,
    int DataRetentionDays,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>
/// Handler for getting current user query.
/// Returns complete user context including tenant and subscription information.
/// </summary>
public class GetCurrentUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GetCurrentUserHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Result<UserDto>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Get user by Zitadel ID
        var user = await _userRepository.GetByZitadelIdAsync(query.ZitadelUserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserDto>(
                Error.NotFound("USER_NOT_FOUND", "User not found. Please complete registration first."));
        }

        // 2. Get associated tenant
        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Result.Failure<UserDto>(
                Error.NotFound("TENANT_NOT_FOUND", "Associated tenant not found."));
        }

        // 3. Get active subscription
        var subscription = await _subscriptionRepository.GetActiveByTenantAsync(user.TenantId, cancellationToken);
        if (subscription == null)
        {
            return Result.Failure<UserDto>(
                Error.NotFound("SUBSCRIPTION_NOT_FOUND", "No active subscription found for tenant."));
        }

        // 4. Update last login timestamp
        user.RecordLogin(DateTimeOffset.UtcNow);
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // 5. Build UserDto with complete context
        var userDto = new UserDto(
            UserId: user.Id.Value,
            TenantId: tenant.Id.Value,
            TenantName: tenant.Name,
            TenantSlug: tenant.Slug,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role,
            Status: user.Status,
            SubscriptionTier: tenant.SubscriptionTier,
            MaxDevices: tenant.MaxDevices,
            CurrentDeviceCount: subscription.DeviceCount,
            DataRetentionDays: tenant.DataRetentionDays,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt);

        return Result.Success(userDto);
    }
}
