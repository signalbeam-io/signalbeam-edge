namespace SignalBeam.Domain.Enums;

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert - no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning - attention needed but not urgent.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Critical - immediate action required.
    /// </summary>
    Critical = 2
}
