namespace SignalBeam.Domain.Enums;

/// <summary>
/// Overall lifecycle status of a rollout.
/// </summary>
public enum RolloutLifecycleStatus
{
    /// <summary>
    /// Rollout created but not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Rollout is actively progressing through phases.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Rollout has been manually paused.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Rollout completed successfully across all phases.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Rollout failed and stopped.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Rollout was rolled back to previous version.
    /// </summary>
    RolledBack = 5,

    /// <summary>
    /// Rollout was cancelled before completion.
    /// </summary>
    Cancelled = 6
}
