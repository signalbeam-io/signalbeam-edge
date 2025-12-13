namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the deployment status of a bundle assignment to a device.
/// </summary>
public enum BundleDeploymentStatus
{
    /// <summary>
    /// Bundle has been assigned but device hasn't received it yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Device is currently downloading and deploying the bundle.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Bundle was successfully deployed and is running.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Bundle deployment failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Bundle deployment was rolled back.
    /// </summary>
    RolledBack = 4
}
