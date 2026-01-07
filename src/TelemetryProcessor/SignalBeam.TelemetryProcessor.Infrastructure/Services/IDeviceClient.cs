using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

/// <summary>
/// Client for retrieving device information from DeviceManager.
/// </summary>
public interface IDeviceClient
{
    /// <summary>
    /// Gets all device IDs for a specific tenant.
    /// </summary>
    Task<Result<IReadOnlyCollection<DeviceId>>> GetDeviceIdsByTenantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
