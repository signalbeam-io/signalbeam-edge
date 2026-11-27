using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Services;

// Who is calling a device endpoint, from the authenticated principal. DeviceId is set only for
// device credentials.
public sealed record DeviceCaller(Guid? TenantId, Guid? DeviceId);

// Checks that a caller may act on a device. A device credential was verified by DeviceManager for
// that exact device; anyone else (operator JWT, dev tenant key) must belong to the device's tenant.
public class DeviceOwnershipVerifier
{
    private readonly IDeviceTenantClient _deviceTenantClient;

    public DeviceOwnershipVerifier(IDeviceTenantClient deviceTenantClient)
    {
        _deviceTenantClient = deviceTenantClient;
    }

    public async Task<Error?> CheckAsync(Guid deviceId, DeviceCaller caller, CancellationToken cancellationToken)
    {
        if (caller.DeviceId is { } callerDeviceId)
        {
            return callerDeviceId == deviceId ? null : DeviceNotFound(deviceId);
        }

        if (caller.TenantId is null || caller.TenantId == Guid.Empty)
        {
            return Error.Forbidden("TENANT_REQUIRED", "The caller has no tenant.");
        }

        var lookup = await _deviceTenantClient.LookupAsync(deviceId, cancellationToken);
        return lookup.Status switch
        {
            DeviceTenantLookupStatus.Unavailable => Error.Failure(
                "DEVICE_LOOKUP_UNAVAILABLE", "The device could not be verified right now. Try again later."),
            // Another tenant's device looks the same as a missing one, so device IDs can't be probed.
            DeviceTenantLookupStatus.NotFound => DeviceNotFound(deviceId),
            _ when lookup.TenantId != caller.TenantId => DeviceNotFound(deviceId),
            _ => null
        };
    }

    private static Error DeviceNotFound(Guid deviceId) =>
        Error.NotFound("DEVICE_NOT_FOUND", $"Device {deviceId} not found.");
}
