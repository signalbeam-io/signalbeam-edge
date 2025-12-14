namespace SignalBeam.Shared.Infrastructure.Time;

/// <summary>
/// Abstraction for getting the current date and time.
/// Useful for testing time-dependent logic.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current date and time in the local time zone.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Gets today's date at midnight UTC.
    /// </summary>
    DateTimeOffset Today { get; }
}
