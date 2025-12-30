using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a calculated health score for a device at a specific point in time.
/// Health score is calculated based on:
/// - Heartbeat recency (0-40 points)
/// - Reconciliation success rate (0-30 points)
/// - Resource utilization (0-30 points)
/// </summary>
public class DeviceHealthScore : Entity<Guid>
{
    private DeviceHealthScore()
    {
        // Required for EF Core
    }

    /// <summary>
    /// Gets the device this health score belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; } = default!;

    /// <summary>
    /// Gets the total health score (0-100).
    /// Higher score indicates better health.
    /// </summary>
    public int TotalScore { get; private set; }

    /// <summary>
    /// Gets the heartbeat component of the health score (0-40).
    /// Based on how recently the device sent a heartbeat.
    /// </summary>
    public int HeartbeatScore { get; private set; }

    /// <summary>
    /// Gets the reconciliation component of the health score (0-30).
    /// Based on the success rate of reconciliation attempts.
    /// </summary>
    public int ReconciliationScore { get; private set; }

    /// <summary>
    /// Gets the resource component of the health score (0-30).
    /// Based on CPU, memory, and disk utilization.
    /// </summary>
    public int ResourceScore { get; private set; }

    /// <summary>
    /// Gets the timestamp when this health score was calculated.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Creates a new device health score.
    /// </summary>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="heartbeatScore">Heartbeat score (0-40).</param>
    /// <param name="reconciliationScore">Reconciliation score (0-30).</param>
    /// <param name="resourceScore">Resource utilization score (0-30).</param>
    /// <param name="timestamp">Timestamp of calculation.</param>
    /// <returns>A new DeviceHealthScore instance.</returns>
    /// <exception cref="ArgumentException">Thrown when score components are out of valid range.</exception>
    public static DeviceHealthScore Create(
        DeviceId deviceId,
        int heartbeatScore,
        int reconciliationScore,
        int resourceScore,
        DateTimeOffset timestamp)
    {
        if (heartbeatScore < 0 || heartbeatScore > 40)
        {
            throw new ArgumentException("Heartbeat score must be between 0 and 40.", nameof(heartbeatScore));
        }

        if (reconciliationScore < 0 || reconciliationScore > 30)
        {
            throw new ArgumentException("Reconciliation score must be between 0 and 30.", nameof(reconciliationScore));
        }

        if (resourceScore < 0 || resourceScore > 30)
        {
            throw new ArgumentException("Resource score must be between 0 and 30.", nameof(resourceScore));
        }

        return new DeviceHealthScore
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            HeartbeatScore = heartbeatScore,
            ReconciliationScore = reconciliationScore,
            ResourceScore = resourceScore,
            TotalScore = heartbeatScore + reconciliationScore + resourceScore,
            Timestamp = timestamp
        };
    }

    /// <summary>
    /// Gets the health status category based on the total score.
    /// </summary>
    /// <returns>Health status: Healthy (70-100), Degraded (40-69), or Critical (0-39).</returns>
    public string GetHealthStatus()
    {
        return TotalScore switch
        {
            >= 70 => "Healthy",
            >= 40 => "Degraded",
            _ => "Critical"
        };
    }

    /// <summary>
    /// Determines if the device is considered unhealthy (score below threshold).
    /// </summary>
    /// <param name="threshold">Score threshold (default: 50).</param>
    /// <returns>True if score is below threshold.</returns>
    public bool IsUnhealthy(int threshold = 50)
    {
        return TotalScore < threshold;
    }
}
