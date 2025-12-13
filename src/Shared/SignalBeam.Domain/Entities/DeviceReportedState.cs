using SignalBeam.Domain.Abstractions;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Entities;

/// <summary>
/// Represents the actual state reported by the device (what is actually running).
/// This is reported by the edge agent to the cloud.
/// </summary>
public class DeviceReportedState : Entity<Guid>
{
    /// <summary>
    /// Device this reported state belongs to.
    /// </summary>
    public DeviceId DeviceId { get; private set; }

    /// <summary>
    /// Bundle currently running on the device.
    /// </summary>
    public BundleId? BundleId { get; private set; }

    /// <summary>
    /// Bundle version currently running.
    /// </summary>
    public BundleVersion? Version { get; private set; }

    /// <summary>
    /// When this state was reported by the device (UTC).
    /// </summary>
    public DateTimeOffset ReportedAt { get; private set; }

    /// <summary>
    /// Running containers reported by the device as JSON.
    /// </summary>
    public string? RunningContainersJson { get; private set; }

    /// <summary>
    /// Any error messages from the device.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    // EF Core constructor
    private DeviceReportedState() : base()
    {
    }

    private DeviceReportedState(
        Guid id,
        DeviceId deviceId,
        DateTimeOffset reportedAt) : base(id)
    {
        DeviceId = deviceId;
        ReportedAt = reportedAt;
    }

    /// <summary>
    /// Factory method to create a new reported state.
    /// </summary>
    public static DeviceReportedState Create(
        DeviceId deviceId,
        DateTimeOffset reportedAt)
    {
        return new DeviceReportedState(Guid.NewGuid(), deviceId, reportedAt);
    }

    /// <summary>
    /// Updates the reported state with bundle information.
    /// </summary>
    public void UpdateState(
        BundleId bundleId,
        BundleVersion version,
        DateTimeOffset reportedAt,
        string? runningContainersJson = null,
        string? errorMessage = null)
    {
        BundleId = bundleId;
        Version = version;
        ReportedAt = reportedAt;
        RunningContainersJson = runningContainersJson;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Reports an error without bundle information.
    /// </summary>
    public void ReportError(string errorMessage, DateTimeOffset reportedAt)
    {
        ErrorMessage = errorMessage;
        ReportedAt = reportedAt;
    }
}
