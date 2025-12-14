using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Time;

namespace SignalBeam.Shared.Infrastructure.Tests.Time;

public class SystemDateTimeProviderTests
{
    [Fact]
    public void UtcNow_ShouldReturnCurrentUtcTime()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = provider.UtcNow;

        // Assert
        var after = DateTimeOffset.UtcNow;
        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Now_ShouldReturnCurrentLocalTime()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();
        var before = DateTimeOffset.Now;

        // Act
        var result = provider.Now;

        // Assert
        var after = DateTimeOffset.Now;
        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Today_ShouldReturnTodayAtMidnightUtc()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();

        // Act
        var result = provider.Today;

        // Assert
        result.Should().Be(DateTimeOffset.UtcNow.Date);
        result.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UtcNow_CalledMultipleTimes_ShouldReturnIncreasingValues()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();

        // Act
        var first = provider.UtcNow;
        Thread.Sleep(10); // Small delay to ensure time passes
        var second = provider.UtcNow;

        // Assert
        second.Should().BeAfter(first);
    }
}
