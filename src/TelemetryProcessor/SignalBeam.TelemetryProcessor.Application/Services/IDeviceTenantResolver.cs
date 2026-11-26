using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Services;

// Telemetry carries only device IDs; DeviceManager owns the device-to-tenant mapping.
public interface IDeviceTenantResolver
{
    // Null when the device is unknown or DeviceManager can't be reached.
    Task<TenantId?> ResolveAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}
