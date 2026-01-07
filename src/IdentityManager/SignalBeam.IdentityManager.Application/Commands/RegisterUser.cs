using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Commands;

/// <summary>
/// Command to register a new user and create their tenant (self-service registration).
/// </summary>
public record RegisterUserCommand(
    string Email,
    string Name,
    string ZitadelUserId,
    string TenantName,
    string TenantSlug);

/// <summary>
/// Response after successful user registration.
/// </summary>
public record RegisterUserResponse(
    Guid UserId,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    SubscriptionTier SubscriptionTier);

/// <summary>
/// Handler for user registration command.
/// Creates user, tenant, and subscription atomically.
/// </summary>
public class RegisterUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public RegisterUserHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Check if user already exists by Zitadel ID
        var existingUser = await _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, cancellationToken);
        if (existingUser != null)
        {
            return Result.Failure<RegisterUserResponse>(
                Error.Conflict("USER_EXISTS", "User with this Zitadel ID is already registered."));
        }

        // 2. Validate tenant slug availability
        if (!TenantSlug.TryParse(command.TenantSlug, out var tenantSlug))
        {
            return Result.Failure<RegisterUserResponse>(
                Error.Validation("INVALID_SLUG", "Tenant slug must be 2-64 characters long and contain only lowercase letters, numbers, and hyphens."));
        }

        var existingTenant = await _tenantRepository.GetBySlugAsync(command.TenantSlug, cancellationToken);
        if (existingTenant != null)
        {
            return Result.Failure<RegisterUserResponse>(
                Error.Conflict("TENANT_SLUG_TAKEN", "This tenant slug is already in use. Please choose a different one."));
        }

        // 3. Create new tenant with Free tier
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(
            tenantId,
            command.TenantName,
            command.TenantSlug,
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        // 4. Create user as tenant Admin
        var userId = UserId.New();
        var user = User.Create(
            userId,
            tenantId,
            command.Email,
            command.Name,
            command.ZitadelUserId,
            UserRole.Admin, // First user is always Admin
            DateTimeOffset.UtcNow);

        // 5. Create subscription with Free tier
        var subscription = Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        // 6. Persist atomically
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _userRepository.AddAsync(user, cancellationToken);
        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        // Save all changes in a single transaction
        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new RegisterUserResponse(
            userId.Value,
            tenantId.Value,
            tenant.Name,
            tenant.Slug,
            tenant.SubscriptionTier));
    }
}
