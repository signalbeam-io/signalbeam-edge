using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a single phase within a rollout (e.g., canary, 25%, 50%, full).
/// </summary>
public class RolloutPhase : Entity<Guid>
{
    private readonly List<RolloutDeviceAssignment> _deviceAssignments = [];

    /// <summary>
    /// Rollout this phase belongs to.
    /// </summary>
    public Guid RolloutId { get; private set; }

    /// <summary>
    /// Phase number (0-indexed, determines order).
    /// </summary>
    public int PhaseNumber { get; private set; }

    /// <summary>
    /// Name of this phase (e.g., "Canary", "Early Adopters").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Target number of devices for this phase.
    /// </summary>
    public int TargetDeviceCount { get; private set; }

    /// <summary>
    /// Target percentage of total devices (if percentage-based).
    /// </summary>
    public decimal? TargetPercentage { get; private set; }

    /// <summary>
    /// Current status of this phase.
    /// </summary>
    public PhaseStatus Status { get; private set; }

    /// <summary>
    /// When this phase started (UTC).
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// When this phase completed (UTC).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Number of devices that succeeded in this phase.
    /// </summary>
    public int SuccessCount { get; private set; }

    /// <summary>
    /// Number of devices that failed in this phase.
    /// </summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// Minimum duration devices must remain healthy before progressing (optional).
    /// </summary>
    public TimeSpan? MinHealthyDuration { get; private set; }

    /// <summary>
    /// Device assignments for this phase.
    /// </summary>
    public IReadOnlyCollection<RolloutDeviceAssignment> DeviceAssignments => _deviceAssignments.AsReadOnly();

    // EF Core constructor
    private RolloutPhase() : base()
    {
    }

    private RolloutPhase(
        Guid id,
        Guid rolloutId,
        int phaseNumber,
        string name,
        int targetDeviceCount,
        decimal? targetPercentage,
        TimeSpan? minHealthyDuration) : base(id)
    {
        RolloutId = rolloutId;
        PhaseNumber = phaseNumber;
        Name = name;
        TargetDeviceCount = targetDeviceCount;
        TargetPercentage = targetPercentage;
        MinHealthyDuration = minHealthyDuration;
        Status = PhaseStatus.Pending;
        SuccessCount = 0;
        FailureCount = 0;
    }

    /// <summary>
    /// Factory method to create a new phase.
    /// </summary>
    public static RolloutPhase Create(
        Guid id,
        Guid rolloutId,
        int phaseNumber,
        string name,
        int targetDeviceCount,
        decimal? targetPercentage,
        TimeSpan? minHealthyDuration)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name cannot be empty.", nameof(name));

        if (phaseNumber < 0)
            throw new ArgumentException("Phase number cannot be negative.", nameof(phaseNumber));

        if (targetDeviceCount < 0)
            throw new ArgumentException("Target device count cannot be negative.", nameof(targetDeviceCount));

        if (targetPercentage is < 0 or > 100)
            throw new ArgumentException("Target percentage must be between 0 and 100.", nameof(targetPercentage));

        return new RolloutPhase(id, rolloutId, phaseNumber, name, targetDeviceCount, targetPercentage, minHealthyDuration);
    }

    /// <summary>
    /// Adds a device assignment to this phase.
    /// </summary>
    public void AddDeviceAssignment(RolloutDeviceAssignment assignment)
    {
        if (Status != PhaseStatus.Pending && Status != PhaseStatus.InProgress)
            throw new InvalidOperationException($"Cannot add device assignments to phase in {Status} status.");

        _deviceAssignments.Add(assignment);
    }

    /// <summary>
    /// Starts this phase.
    /// </summary>
    public void Start(DateTimeOffset startedAt)
    {
        if (Status != PhaseStatus.Pending)
            throw new InvalidOperationException($"Cannot start phase in {Status} status.");

        Status = PhaseStatus.InProgress;
        StartedAt = startedAt;
    }

    /// <summary>
    /// Marks this phase as completed.
    /// </summary>
    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != PhaseStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete phase in {Status} status.");

        Status = PhaseStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Marks this phase as failed.
    /// </summary>
    public void Fail()
    {
        if (Status == PhaseStatus.Completed)
            throw new InvalidOperationException("Cannot fail a completed phase.");

        Status = PhaseStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this phase as skipped.
    /// </summary>
    public void Skip()
    {
        if (Status == PhaseStatus.Completed || Status == PhaseStatus.InProgress)
            throw new InvalidOperationException($"Cannot skip phase in {Status} status.");

        Status = PhaseStatus.Skipped;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Increments success count.
    /// </summary>
    public void IncrementSuccessCount()
    {
        SuccessCount++;
    }

    /// <summary>
    /// Increments failure count.
    /// </summary>
    public void IncrementFailureCount()
    {
        FailureCount++;
    }

    /// <summary>
    /// Calculates the success rate for this phase.
    /// </summary>
    public decimal GetSuccessRate()
    {
        var total = SuccessCount + FailureCount;
        return total > 0 ? (decimal)SuccessCount / total : 0;
    }

    /// <summary>
    /// Calculates the failure rate for this phase.
    /// </summary>
    public decimal GetFailureRate()
    {
        var total = SuccessCount + FailureCount;
        return total > 0 ? (decimal)FailureCount / total : 0;
    }

    /// <summary>
    /// Checks if this phase has met its target device count.
    /// </summary>
    public bool HasMetTargetDeviceCount()
    {
        return (SuccessCount + FailureCount) >= TargetDeviceCount;
    }

    /// <summary>
    /// Checks if this phase is healthy based on success/failure rates.
    /// </summary>
    public bool IsHealthy(decimal failureThreshold)
    {
        var failureRate = GetFailureRate();
        return failureRate <= failureThreshold;
    }
}
