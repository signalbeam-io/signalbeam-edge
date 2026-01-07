using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Services;

/// <summary>
/// Client for retrieving tenant retention policies from IdentityManager.
/// </summary>
public interface ITenantRetentionClient
{
    /// <summary>
    /// Gets all tenants with their data retention policies.
    /// </summary>
    Task<Result<IReadOnlyCollection<TenantRetentionInfo>>> GetAllTenantsWithRetentionAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant retention policy information.
/// </summary>
public record TenantRetentionInfo(
    Guid TenantId,
    string TenantName,
    int DataRetentionDays);
