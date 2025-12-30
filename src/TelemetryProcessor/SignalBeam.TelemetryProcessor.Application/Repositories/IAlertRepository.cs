using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.TelemetryProcessor.Application.Repositories;

/// <summary>
/// Repository for managing alerts.
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Adds a new alert.
    /// </summary>
    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an alert by ID.
    /// </summary>
    Task<Alert?> FindByIdAsync(Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing alert.
    /// </summary>
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active alert for a specific device and type (for deduplication).
    /// </summary>
    Task<Alert?> GetActiveAlertByDeviceAndTypeAsync(
        DeviceId deviceId,
        AlertType alertType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active alerts for a tenant.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active alerts by severity.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetActiveAlertsBySeverityAsync(
        AlertSeverity severity,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all alerts for a specific device.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetAlertsByDeviceIdAsync(
        DeviceId deviceId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets alerts created within a time range.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetAlertsByTimeRangeAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of active alerts by type.
    /// </summary>
    Task<Dictionary<AlertType, int>> GetActiveAlertCountsByTypeAsync(
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-resolves alerts when the underlying condition is fixed.
    /// For example, resolves DeviceOffline alerts when device comes back online.
    /// </summary>
    Task<int> AutoResolveAlertsAsync(
        DeviceId deviceId,
        AlertType alertType,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets alerts that have been active for longer than the specified duration.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetStaleAlertsAsync(
        TimeSpan activeDuration,
        TenantId? tenantId = null,
        CancellationToken cancellationToken = default);
}
