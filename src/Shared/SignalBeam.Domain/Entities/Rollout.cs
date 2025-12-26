using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents a phased rollout of a bundle version to devices.
/// Orchestrates deployment across multiple phases with health monitoring.
/// </summary>
public class Rollout : AggregateRoot<Guid>
{
    private readonly List<RolloutPhase> _phases = [];

    /// <summary>
    /// Tenant this rollout belongs to.
    /// </summary>
    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Bundle being rolled out.
    /// </summary>
    public BundleId BundleId { get; private set; }

    /// <summary>
    /// Version being deployed.
    /// </summary>
    public BundleVersion TargetVersion { get; private set; }

    /// <summary>
    /// Previous version (for rollback).
    /// </summary>
    public BundleVersion? PreviousVersion { get; private set; }

    /// <summary>
    /// Human-readable name for this rollout.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of the rollout.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Current status of the rollout.
    /// </summary>
    public RolloutLifecycleStatus Status { get; private set; }

    /// <summary>
    /// Optional target device group (null means all devices).
    /// </summary>
    public Guid? TargetDeviceGroupId { get; private set; }

    /// <summary>
    /// User who created the rollout.
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// When the rollout was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the rollout was started (UTC).
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// When the rollout completed or failed (UTC).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Failure threshold (0.0 to 1.0) - percentage of failures that triggers automatic pause.
    /// Default is 0.05 (5% failure rate).
    /// </summary>
    public decimal FailureThreshold { get; private set; }

    /// <summary>
    /// Current phase number (0-indexed).
    /// </summary>
    public int CurrentPhaseNumber { get; private set; }

    /// <summary>
    /// Phases in this rollout.
    /// </summary>
    public IReadOnlyCollection<RolloutPhase> Phases => _phases.AsReadOnly();

    // EF Core constructor
    private Rollout() : base()
    {
        TenantId = default!;
        BundleId = default!;
        TargetVersion = null!;
    }

    private Rollout(
        Guid id,
        TenantId tenantId,
        BundleId bundleId,
        BundleVersion targetVersion,
        BundleVersion? previousVersion,
        string name,
        string? description,
        Guid? targetDeviceGroupId,
        string? createdBy,
        decimal failureThreshold,
        DateTimeOffset createdAt) : base(id)
    {
        TenantId = tenantId;
        BundleId = bundleId;
        TargetVersion = targetVersion;
        PreviousVersion = previousVersion;
        Name = name;
        Description = description;
        Status = RolloutLifecycleStatus.Pending;
        TargetDeviceGroupId = targetDeviceGroupId;
        CreatedBy = createdBy;
        FailureThreshold = failureThreshold;
        CreatedAt = createdAt;
        CurrentPhaseNumber = 0;
    }

    /// <summary>
    /// Factory method to create a new rollout.
    /// </summary>
    public static Rollout Create(
        Guid id,
        TenantId tenantId,
        BundleId bundleId,
        BundleVersion targetVersion,
        BundleVersion? previousVersion,
        string name,
        string? description,
        Guid? targetDeviceGroupId,
        string? createdBy,
        decimal failureThreshold,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rollout name cannot be empty.", nameof(name));

        if (failureThreshold < 0 || failureThreshold > 1)
            throw new ArgumentException("Failure threshold must be between 0 and 1.", nameof(failureThreshold));

        var rollout = new Rollout(
            id,
            tenantId,
            bundleId,
            targetVersion,
            previousVersion,
            name,
            description,
            targetDeviceGroupId,
            createdBy,
            failureThreshold,
            createdAt);

        return rollout;
    }

    /// <summary>
    /// Adds a phase to the rollout.
    /// </summary>
    public void AddPhase(RolloutPhase phase)
    {
        if (Status != RolloutLifecycleStatus.Pending)
            throw new InvalidOperationException("Cannot add phases to a rollout that has already started.");

        _phases.Add(phase);
    }

    /// <summary>
    /// Starts the rollout.
    /// </summary>
    public void Start(DateTimeOffset startedAt)
    {
        if (Status != RolloutLifecycleStatus.Pending && Status != RolloutLifecycleStatus.Paused)
            throw new InvalidOperationException($"Cannot start rollout in {Status} status.");

        if (_phases.Count == 0)
            throw new InvalidOperationException("Cannot start rollout without phases.");

        Status = RolloutLifecycleStatus.InProgress;
        StartedAt = startedAt;

        RaiseDomainEvent(new RolloutStartedEvent(Id, TenantId, BundleId, TargetVersion, startedAt));
    }

    /// <summary>
    /// Pauses the rollout.
    /// </summary>
    public void Pause()
    {
        if (Status != RolloutLifecycleStatus.InProgress)
            throw new InvalidOperationException($"Cannot pause rollout in {Status} status.");

        Status = RolloutLifecycleStatus.Paused;

        RaiseDomainEvent(new RolloutPausedEvent(Id, TenantId, CurrentPhaseNumber));
    }

    /// <summary>
    /// Resumes a paused rollout.
    /// </summary>
    public void Resume(DateTimeOffset resumedAt)
    {
        if (Status != RolloutLifecycleStatus.Paused)
            throw new InvalidOperationException($"Cannot resume rollout in {Status} status.");

        Status = RolloutLifecycleStatus.InProgress;

        RaiseDomainEvent(new RolloutResumedEvent(Id, TenantId, CurrentPhaseNumber, resumedAt));
    }

    /// <summary>
    /// Advances to the next phase.
    /// </summary>
    public void AdvancePhase()
    {
        if (Status != RolloutLifecycleStatus.InProgress)
            throw new InvalidOperationException($"Cannot advance phase when rollout is {Status}.");

        if (CurrentPhaseNumber >= _phases.Count - 1)
            throw new InvalidOperationException("Already at the last phase.");

        var currentPhase = _phases[CurrentPhaseNumber];
        if (currentPhase.Status != PhaseStatus.Completed)
            throw new InvalidOperationException("Cannot advance to next phase until current phase is completed.");

        CurrentPhaseNumber++;

        RaiseDomainEvent(new RolloutPhaseAdvancedEvent(Id, TenantId, CurrentPhaseNumber));
    }

    /// <summary>
    /// Marks the current phase as started.
    /// </summary>
    public void StartCurrentPhase(DateTimeOffset startedAt)
    {
        if (Status != RolloutLifecycleStatus.InProgress)
            throw new InvalidOperationException($"Cannot start phase when rollout is {Status}.");

        var currentPhase = _phases[CurrentPhaseNumber];
        currentPhase.Start(startedAt);

        RaiseDomainEvent(new RolloutPhaseStartedEvent(Id, TenantId, currentPhase.Id, CurrentPhaseNumber, startedAt));
    }

    /// <summary>
    /// Marks the current phase as completed.
    /// </summary>
    public void CompleteCurrentPhase(DateTimeOffset completedAt)
    {
        if (Status != RolloutLifecycleStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete phase when rollout is {Status}.");

        var currentPhase = _phases[CurrentPhaseNumber];
        currentPhase.Complete(completedAt);

        RaiseDomainEvent(new RolloutPhaseCompletedEvent(Id, TenantId, currentPhase.Id, CurrentPhaseNumber, completedAt));

        // If this was the last phase, complete the rollout
        if (CurrentPhaseNumber == _phases.Count - 1)
        {
            Complete(completedAt);
        }
    }

    /// <summary>
    /// Marks the current phase as failed.
    /// </summary>
    public void FailCurrentPhase()
    {
        var currentPhase = _phases[CurrentPhaseNumber];
        currentPhase.Fail();

        Fail(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Completes the rollout successfully.
    /// </summary>
    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != RolloutLifecycleStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete rollout in {Status} status.");

        Status = RolloutLifecycleStatus.Completed;
        CompletedAt = completedAt;

        RaiseDomainEvent(new RolloutCompletedEvent(Id, TenantId, BundleId, TargetVersion, completedAt));
    }

    /// <summary>
    /// Marks the rollout as failed.
    /// </summary>
    public void Fail(DateTimeOffset failedAt)
    {
        if (Status == RolloutLifecycleStatus.Completed || Status == RolloutLifecycleStatus.RolledBack)
            throw new InvalidOperationException($"Cannot fail rollout in {Status} status.");

        Status = RolloutLifecycleStatus.Failed;
        CompletedAt = failedAt;

        RaiseDomainEvent(new RolloutFailedEvent(Id, TenantId, CurrentPhaseNumber, failedAt));
    }

    /// <summary>
    /// Initiates rollback to previous version.
    /// </summary>
    public void Rollback(DateTimeOffset rolledBackAt)
    {
        if (PreviousVersion == null)
            throw new InvalidOperationException("Cannot rollback - no previous version specified.");

        if (Status == RolloutLifecycleStatus.Completed)
            throw new InvalidOperationException("Cannot rollback a completed rollout.");

        Status = RolloutLifecycleStatus.RolledBack;
        CompletedAt = rolledBackAt;

        RaiseDomainEvent(new RolloutRolledBackEvent(Id, TenantId, BundleId, TargetVersion, PreviousVersion, rolledBackAt));
    }

    /// <summary>
    /// Cancels the rollout.
    /// </summary>
    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == RolloutLifecycleStatus.Completed || Status == RolloutLifecycleStatus.RolledBack)
            throw new InvalidOperationException($"Cannot cancel rollout in {Status} status.");

        Status = RolloutLifecycleStatus.Cancelled;
        CompletedAt = cancelledAt;

        RaiseDomainEvent(new RolloutCancelledEvent(Id, TenantId, cancelledAt));
    }

    /// <summary>
    /// Gets the current phase.
    /// </summary>
    public RolloutPhase? GetCurrentPhase()
    {
        if (CurrentPhaseNumber >= _phases.Count)
            return null;

        return _phases[CurrentPhaseNumber];
    }

    /// <summary>
    /// Calculates overall success/failure rate across all phases.
    /// </summary>
    public (int total, int succeeded, int failed, decimal successRate) GetOverallProgress()
    {
        var total = _phases.Sum(p => p.SuccessCount + p.FailureCount);
        var succeeded = _phases.Sum(p => p.SuccessCount);
        var failed = _phases.Sum(p => p.FailureCount);
        var successRate = total > 0 ? (decimal)succeeded / total : 0;

        return (total, succeeded, failed, successRate);
    }
}
