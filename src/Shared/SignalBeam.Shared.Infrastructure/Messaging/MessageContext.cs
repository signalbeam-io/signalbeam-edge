namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// Provides context information for a received message.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Gets the message ID.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subject/topic the message was received on.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Gets the correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the timestamp when the message was published.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets custom headers associated with the message.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the sequence number of the message (for JetStream/persistent messages).
    /// </summary>
    public long? Sequence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message requires acknowledgment.
    /// </summary>
    public bool RequiresAck { get; init; }
}
