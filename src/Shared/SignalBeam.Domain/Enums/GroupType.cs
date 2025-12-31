namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the type of device group.
/// </summary>
public enum GroupType
{
    /// <summary>
    /// Static group - devices are manually added/removed.
    /// </summary>
    Static = 0,

    /// <summary>
    /// Dynamic group - devices are automatically added/removed based on tag query.
    /// </summary>
    Dynamic = 1
}
