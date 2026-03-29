namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Listens for bundle assignment changes pushed from the cloud via NATS.
/// The host layer wires the concrete implementation (NATS subscriber)
/// and triggers reconciliation when an assignment arrives.
/// </summary>
public interface IAssignmentListener
{
    /// <summary>
    /// Starts listening for assignment messages on the device's NATS subject.
    /// Returns immediately; messages are delivered via <see cref="OnAssignmentReceived"/>.
    /// </summary>
    Task StartListeningAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening and cleans up the subscription.
    /// </summary>
    Task StopListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when a new bundle assignment is received for this device.
    /// </summary>
    event EventHandler<AssignmentReceivedEventArgs> OnAssignmentReceived;
}

public sealed class AssignmentReceivedEventArgs : EventArgs
{
    public required Guid DeviceId { get; init; }
    public required string BundleId { get; init; }
    public required string BundleVersion { get; init; }
    public required DateTimeOffset AssignedAt { get; init; }
}
