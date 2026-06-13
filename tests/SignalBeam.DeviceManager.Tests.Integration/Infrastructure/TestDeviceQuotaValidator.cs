using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

/// <summary>
/// Test implementation of IDeviceQuotaValidator that always grants quota.
/// Avoids a real HTTP call to IdentityManager (which is not running in the
/// integration test environment and would otherwise fail with connection refused).
/// </summary>
public class TestDeviceQuotaValidator : IDeviceQuotaValidator
{
    public Task<Result> CheckDeviceQuotaAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
