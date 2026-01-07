using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Queries;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.IdentityManager.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Domain.Enums;

namespace SignalBeam.IdentityManager.Host.Endpoints;

/// <summary>
/// Subscription management endpoints.
/// </summary>
public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization()
            .WithOpenApi();

        group.MapGet("/", GetSubscription)
            .WithName("GetSubscription")
            .WithSummary("Get current subscription")
            .WithDescription("Returns the current subscription details for the authenticated user's tenant.")
            .Produces<SubscriptionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/upgrade", UpgradeSubscription)
            .WithName("UpgradeSubscription")
            .WithSummary("Upgrade subscription tier")
            .WithDescription("Upgrades the tenant's subscription tier. Requires Admin role.")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .Produces<UpgradeSubscriptionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/check-device-quota", CheckDeviceQuota)
            .AllowAnonymous() // Allow service-to-service calls without auth
            .WithName("CheckDeviceQuota")
            .WithSummary("Check if tenant can add another device")
            .WithDescription("Validates if the tenant has quota available to register a new device. Used for internal service-to-service communication.")
            .Produces<CheckDeviceQuotaResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>
    /// Get current subscription for the authenticated user's tenant.
    /// GET /api/subscriptions
    /// </summary>
    private static async Task<IResult> GetSubscription(
        [FromServices] ITenantRepository tenantRepository,
        [FromServices] ISubscriptionRepository subscriptionRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Get tenant ID from user claims (set by GetCurrentUser or middleware)
        var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value;

        // If no tenant_id claim, try to get it from the Zitadel user ID
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            var zitadelUserId = httpContext.User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(zitadelUserId))
            {
                return Results.Unauthorized();
            }

            // Get user to find tenant ID
            var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByZitadelIdAsync(zitadelUserId, cancellationToken);
            if (user == null)
            {
                return Results.NotFound(new { error = "USER_NOT_FOUND", message = "User not found." });
            }
            tenantIdClaim = user.TenantId.Value.ToString();
        }

        if (!Guid.TryParse(tenantIdClaim, out var tenantIdGuid))
        {
            return Results.BadRequest(new { error = "INVALID_TENANT_ID", message = "Invalid tenant ID in claims." });
        }

        var tenantId = new TenantId(tenantIdGuid);

        // Get tenant
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            return Results.NotFound(new { error = "TENANT_NOT_FOUND", message = "Tenant not found." });
        }

        // Get active subscription
        var subscription = await subscriptionRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (subscription == null)
        {
            return Results.NotFound(new { error = "SUBSCRIPTION_NOT_FOUND", message = "No active subscription found." });
        }

        var dto = new SubscriptionDto(
            SubscriptionId: subscription.Id,
            TenantId: tenant.Id.Value,
            TenantName: tenant.Name,
            Tier: subscription.Tier,
            Status: subscription.Status,
            MaxDevices: tenant.MaxDevices,
            CurrentDeviceCount: subscription.DeviceCount,
            DataRetentionDays: tenant.DataRetentionDays,
            StartedAt: subscription.StartedAt,
            UpgradedAt: tenant.UpgradedAt);

        return Results.Ok(dto);
    }

    /// <summary>
    /// Upgrade subscription tier.
    /// POST /api/subscriptions/upgrade
    /// </summary>
    private static async Task<IResult> UpgradeSubscription(
        [FromBody] UpgradeSubscriptionRequest request,
        [FromServices] UpgradeSubscriptionHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Get user ID from claims
        var userIdClaim = httpContext.User.FindFirst("user_id")?.Value;

        // If no user_id claim, try to get it from Zitadel user ID
        if (string.IsNullOrEmpty(userIdClaim))
        {
            var zitadelUserId = httpContext.User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(zitadelUserId))
            {
                return Results.Unauthorized();
            }

            // Get user to find user ID
            var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByZitadelIdAsync(zitadelUserId, cancellationToken);
            if (user == null)
            {
                return Results.NotFound(new { error = "USER_NOT_FOUND", message = "User not found." });
            }
            userIdClaim = user.Id.Value.ToString();
        }

        if (!Guid.TryParse(userIdClaim, out var userIdGuid))
        {
            return Results.BadRequest(new { error = "INVALID_USER_ID", message = "Invalid user ID in claims." });
        }

        // Get tenant ID from claims
        var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(tenantIdClaim, out var tenantIdGuid))
        {
            return Results.BadRequest(new { error = "INVALID_TENANT_ID", message = "Invalid tenant ID in claims." });
        }

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantIdGuid,
            NewTier: request.NewTier,
            UpgradedByUserId: userIdGuid);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Validation =>
                    Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message }),
                SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound =>
                    Results.NotFound(new { error = result.Error.Code, message = result.Error.Message }),
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Forbidden =>
                    Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Problem(
                    title: "Upgrade failed",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.Ok(result.Value);
    }

    /// <summary>
    /// Check if tenant can add another device.
    /// POST /api/subscriptions/check-device-quota
    /// </summary>
    private static async Task<IResult> CheckDeviceQuota(
        [FromBody] CheckDeviceQuotaRequest request,
        [FromServices] IQuotaEnforcementService quotaService,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(request.TenantId);

        var result = await quotaService.CheckDeviceQuotaAsync(tenantId, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.Type switch
            {
                SignalBeam.Shared.Infrastructure.Results.ErrorType.Validation =>
                    Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message }),
                SignalBeam.Shared.Infrastructure.Results.ErrorType.NotFound =>
                    Results.NotFound(new { error = result.Error.Code, message = result.Error.Message }),
                _ => Results.Problem(
                    title: "Quota check failed",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        return Results.Ok(new CheckDeviceQuotaResponse(true, "Device quota available"));
    }
}

/// <summary>
/// Request to check device quota.
/// </summary>
public record CheckDeviceQuotaRequest(Guid TenantId);

/// <summary>
/// Response for device quota check.
/// </summary>
public record CheckDeviceQuotaResponse(bool CanAddDevice, string Message);

/// <summary>
/// Request to upgrade subscription.
/// </summary>
public record UpgradeSubscriptionRequest(SubscriptionTier NewTier);

/// <summary>
/// Subscription information DTO.
/// </summary>
public record SubscriptionDto(
    Guid SubscriptionId,
    Guid TenantId,
    string TenantName,
    SubscriptionTier Tier,
    SubscriptionStatus Status,
    int MaxDevices,
    int CurrentDeviceCount,
    int DataRetentionDays,
    DateTimeOffset StartedAt,
    DateTimeOffset? UpgradedAt);
