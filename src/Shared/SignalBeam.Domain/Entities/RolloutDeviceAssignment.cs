using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Tracks the assignment of a device to a specific rollout phase.
/// </summary>
public class RolloutDeviceAssignment : Entity<Guid>
{
    /// <summary>
    /// Rollout this assignment belongs to.
    /// </summary>
    public Guid RolloutId { get; private set; }

    /// <summary>
    /// Phase this device is assigned to.
    /// </summary>
    public Guid PhaseId { get; private set; }

    /// <summary>
    /// Device being assigned.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Current status of this assignment.
    /// </summary>
    public DeviceAssignmentStatus Status { get; private set; }

    /// <summary>
    /// When the device was assigned (UTC).
    /// </summary>
    public DateTimeOffset? AssignedAt { get; private set; }

    /// <summary>
    /// When the device reconciled successfully (UTC).
    /// </summary>
    public DateTimeOffset? ReconciledAt { get; private set; }

    /// <summary>
    /// Error message if assignment failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; private set; }

    // EF Core constructor
    private RolloutDeviceAssignment() : base()
    {
        DeviceId = default!;
    }

    private RolloutDeviceAssignment(
        Guid id,
        Guid rolloutId,
        Guid phaseId,
        DeviceId deviceId) : base(id)
    {
        RolloutId = rolloutId;
        PhaseId = phaseId;
        DeviceId = deviceId;
        Status = DeviceAssignmentStatus.Pending;
        RetryCount = 0;
    }

    /// <summary>
    /// Factory method to create a new device assignment.
    /// </summary>
    public static RolloutDeviceAssignment Create(
        Guid id,
        Guid rolloutId,
        Guid phaseId,
        DeviceId deviceId)
    {
        return new RolloutDeviceAssignment(id, rolloutId, phaseId, deviceId);
    }

    /// <summary>
    /// Marks the device as assigned (desired state updated).
    /// </summary>
    public void MarkAssigned(DateTimeOffset assignedAt)
    {
        if (Status != DeviceAssignmentStatus.Pending)
            throw new InvalidOperationException($"Cannot mark as assigned when status is {Status}.");

        Status = DeviceAssignmentStatus.Assigned;
        AssignedAt = assignedAt;
    }

    /// <summary>
    /// Marks the device as reconciling.
    /// </summary>
    public void MarkReconciling()
    {
        if (Status != DeviceAssignmentStatus.Assigned && Status != DeviceAssignmentStatus.Pending)
            throw new InvalidOperationException($"Cannot mark as reconciling when status is {Status}.");

        Status = DeviceAssignmentStatus.Reconciling;
    }

    /// <summary>
    /// Marks the assignment as succeeded.
    /// </summary>
    public void MarkSucceeded(DateTimeOffset reconciledAt)
    {
        if (Status == DeviceAssignmentStatus.Succeeded || Status == DeviceAssignmentStatus.Failed)
            throw new InvalidOperationException($"Cannot mark as succeeded when status is {Status}.");

        Status = DeviceAssignmentStatus.Succeeded;
        ReconciledAt = reconciledAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the assignment as failed.
    /// </summary>
    public void MarkFailed(string errorMessage, DateTimeOffset failedAt)
    {
        if (Status == DeviceAssignmentStatus.Succeeded)
            throw new InvalidOperationException("Cannot mark as failed when already succeeded.");

        Status = DeviceAssignmentStatus.Failed;
        ErrorMessage = errorMessage;
        ReconciledAt = failedAt;
    }

    /// <summary>
    /// Increments retry count and resets to pending.
    /// </summary>
    public void Retry()
    {
        if (Status == DeviceAssignmentStatus.Succeeded)
            throw new InvalidOperationException("Cannot retry a succeeded assignment.");

        RetryCount++;
        Status = DeviceAssignmentStatus.Pending;
        ErrorMessage = null;
    }

    /// <summary>
    /// Checks if this assignment can be retried.
    /// </summary>
    public bool CanRetry(int maxRetries)
    {
        return RetryCount < maxRetries && Status == DeviceAssignmentStatus.Failed;
    }
}
