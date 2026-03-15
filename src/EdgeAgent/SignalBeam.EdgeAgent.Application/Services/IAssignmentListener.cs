namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Abstraction for listening to bundle assignment push notifications.
/// </summary>
public interface IAssignmentListener
{
    /// <summary>
    /// Raised when a bundle assignment message is received for this device.
    /// </summary>
    event Func<BundleAssignmentMessage, Task>? OnAssignmentReceived;

    /// <summary>
    /// Starts listening for assignment messages for the specified device.
    /// </summary>
    Task StartAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Stops listening for assignment messages.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Assignment message schema matching cloud-side BundleAssignedEvent.
/// Published to JetStream stream BUNDLE_ASSIGNMENTS.
/// </summary>
public record BundleAssignmentMessage(
    Guid DeviceId,
    Guid BundleId,
    string BundleVersion,
    DateTimeOffset AssignedAt);
