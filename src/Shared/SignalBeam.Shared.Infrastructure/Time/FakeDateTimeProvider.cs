namespace SignalBeam.Shared.Infrastructure.Time;

/// <summary>
/// Fake implementation of <see cref="IDateTimeProvider"/> for testing.
/// Allows setting a specific date and time.
/// </summary>
public sealed class FakeDateTimeProvider : IDateTimeProvider
{
    private DateTimeOffset _currentTime;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeDateTimeProvider"/> with the current UTC time.
    /// </summary>
    public FakeDateTimeProvider()
    {
        _currentTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FakeDateTimeProvider"/> with the specified time.
    /// </summary>
    public FakeDateTimeProvider(DateTimeOffset currentTime)
    {
        _currentTime = currentTime;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _currentTime.ToUniversalTime();

    /// <inheritdoc />
    public DateTimeOffset Now => _currentTime;

    /// <inheritdoc />
    public DateTimeOffset Today => _currentTime.Date;

    /// <summary>
    /// Sets the current time to the specified value.
    /// </summary>
    public void SetTime(DateTimeOffset time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }

    /// <summary>
    /// Resets the time to the current system time.
    /// </summary>
    public void Reset()
    {
        _currentTime = DateTimeOffset.UtcNow;
    }
}
