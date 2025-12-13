namespace SignalBeam.Domain.Abstractions;

/// <summary>
/// Base class for domain events.
/// Domain events represent something that happened in the domain.
/// </summary>
public abstract record DomainEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When this event occurred (UTC).
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Event type name (automatically derived from class name).
    /// </summary>
    public string EventType => GetType().Name;
}
