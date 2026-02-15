namespace SignalBeam.Domain.Enums;

/// <summary>
/// Status of a user invitation within a tenant.
/// </summary>
public enum InvitationStatus
{
    /// <summary>
    /// Invitation has been sent and is awaiting response.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Invitation was accepted by the recipient.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// Invitation was declined by the recipient.
    /// </summary>
    Declined = 2,

    /// <summary>
    /// Invitation has expired without a response.
    /// </summary>
    Expired = 3
}
