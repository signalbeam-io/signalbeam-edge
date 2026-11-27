using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Services;

public static class DeviceGroupErrors
{
    // The error for a lookup that didn't find the group (or found another tenant's).
    // Unavailable maps to 503, so callers can retry once DeviceManager is reachable again.
    public static Error From(DeviceGroupLookup lookup, Guid groupId) =>
        lookup.Status == DeviceGroupLookupStatus.Unavailable
            ? Error.Failure("DEVICE_GROUP_LOOKUP_UNAVAILABLE", "The device group could not be loaded right now. Try again later.")
            : Error.NotFound("GROUP_NOT_FOUND", $"Device group {groupId} not found.");
}
