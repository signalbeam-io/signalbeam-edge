using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Services;

/// <summary>
/// Service for enforcing subscription quotas (device limits, data retention).
/// </summary>
public interface IQuotaEnforcementService
{
    /// <summary>
    /// Checks if a tenant can add a new device based on their subscription tier quota.
    /// </summary>
    /// <param name="tenantId">The tenant ID to check quota for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if within quota, Failure with error if quota exceeded.</returns>
    Task<Result> CheckDeviceQuotaAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current device count for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current device count.</returns>
    Task<int> GetCurrentDeviceCountAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces data retention policy for a tenant by marking old data for deletion.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if retention enforced, Failure with error otherwise.</returns>
    Task<Result> EnforceDataRetentionAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
