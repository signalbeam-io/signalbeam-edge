namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the type of device group membership.
/// </summary>
public enum MembershipType
{
    /// <summary>
    /// Static membership - device was manually added to the group.
    /// </summary>
    Static = 0,

    /// <summary>
    /// Dynamic membership - device was automatically added based on tag query.
    /// </summary>
    Dynamic = 1
}
