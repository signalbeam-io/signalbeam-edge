using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.TelemetryProcessor.Application.Services;

/// <summary>
/// Service for enforcing data retention policies based on tenant subscription tiers.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Deletes metrics and heartbeats older than the retention policy for all tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with total deleted record counts.</returns>
    Task<Result<DataRetentionResult>> EnforceDataRetentionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of data retention enforcement.
/// </summary>
public record DataRetentionResult(
    int TenantsProcessed,
    int MetricsDeleted,
    int HeartbeatsDeleted,
    TimeSpan ElapsedTime);
