namespace SignalBeam.Domain.Enums;

/// <summary>
/// Status of a rollout phase.
/// </summary>
public enum PhaseStatus
{
    /// <summary>
    /// Phase not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Phase is actively deploying to devices.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Phase completed successfully, ready for next phase.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Phase failed due to too many device failures.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Phase was skipped (e.g., during rollback).
    /// </summary>
    Skipped = 4
}
