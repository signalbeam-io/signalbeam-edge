using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Tracks the rollout progress of a bundle to devices.
/// </summary>
public class RolloutStatus : Entity<Guid>
{
    /// <summary>
    /// Bundle being rolled out.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Version being rolled out.
    /// </summary>
    public BundleVersion BundleVersion { get; private set; }

    /// <summary>
    /// Device this rollout applies to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Current status of the rollout.
    /// </summary>
    public RolloutState Status { get; private set; }

    /// <summary>
    /// When the rollout started (UTC).
    /// </summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// When the rollout completed or failed (UTC).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Error message if rollout failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; private set; }

    // EF Core constructor
    private RolloutStatus() : base()
    {
        BundleId = default;
        BundleVersion = null!;
        DeviceId = default;
    }

    private RolloutStatus(
        Guid id,
        BundleId bundleId,
        BundleVersion bundleVersion,
        DeviceId deviceId,
        DateTimeOffset startedAt) : base(id)
    {
        BundleId = bundleId;
        BundleVersion = bundleVersion;
        DeviceId = deviceId;
        Status = RolloutState.Pending;
        StartedAt = startedAt;
        RetryCount = 0;
    }

    /// <summary>
    /// Factory method to create a new rollout status.
    /// </summary>
    public static RolloutStatus Create(
        Guid id,
        BundleId bundleId,
        BundleVersion bundleVersion,
        DeviceId deviceId,
        DateTimeOffset startedAt)
    {
        return new RolloutStatus(id, bundleId, bundleVersion, deviceId, startedAt);
    }

    /// <summary>
    /// Marks rollout as in progress.
    /// </summary>
    public void MarkInProgress()
    {
        if (Status == RolloutState.Succeeded || Status == RolloutState.Failed)
            throw new InvalidOperationException($"Cannot mark rollout as in progress when it is already {Status}.");

        Status = RolloutState.InProgress;
    }

    /// <summary>
    /// Marks rollout as succeeded.
    /// </summary>
    public void MarkSucceeded(DateTimeOffset completedAt)
    {
        Status = RolloutState.Succeeded;
        CompletedAt = completedAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks rollout as failed.
    /// </summary>
    public void MarkFailed(string errorMessage, DateTimeOffset completedAt)
    {
        Status = RolloutState.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Increments retry count.
    /// </summary>
    public void IncrementRetryCount()
    {
        RetryCount++;
        Status = RolloutState.Pending;
        CompletedAt = null;
        ErrorMessage = null;
    }
}

/// <summary>
/// Rollout state enumeration.
/// </summary>
public enum RolloutState
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3
}
