namespace SignalBeam.Domain.Enums;

/// <summary>
/// Status of a bundle version.
/// </summary>
public enum BundleStatus
{
    /// <summary>
    /// Bundle is being created and is not yet ready for deployment.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Bundle is published and ready for deployment to devices.
    /// </summary>
    Published = 1,

    /// <summary>
    /// Bundle is deprecated and should not be used for new deployments.
    /// </summary>
    Deprecated = 2
}
