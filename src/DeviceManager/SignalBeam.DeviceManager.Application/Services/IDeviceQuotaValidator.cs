using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Application.Services;

/// <summary>
/// Service for validating device quotas.
/// </summary>
public interface IDeviceQuotaValidator
{
    /// <summary>
    /// Checks if the tenant can add another device based on their subscription quota.
    /// </summary>
    /// <param name="tenantId">The tenant ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if quota available, Failure with error details if quota exceeded.</returns>
    Task<Result> CheckDeviceQuotaAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
