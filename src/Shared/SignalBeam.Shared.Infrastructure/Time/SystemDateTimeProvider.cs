namespace SignalBeam.Shared.Infrastructure.Time;

/// <summary>
/// Production implementation of <see cref="IDateTimeProvider"/> using system time.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <inheritdoc />
    public DateTimeOffset Today => DateTimeOffset.UtcNow.Date;
}
