using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.TelemetryProcessor.Application.Services;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

public sealed class DeviceTenantResolver : IDeviceTenantResolver
{
    private readonly IDeviceTenantClient _client;

    public DeviceTenantResolver(IDeviceTenantClient client)
    {
        _client = client;
    }

    public async Task<TenantId?> ResolveAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        var lookup = await _client.LookupAsync(deviceId.Value, cancellationToken);
        return lookup.Status == DeviceTenantLookupStatus.Found ? new TenantId(lookup.TenantId) : null;
    }
}
