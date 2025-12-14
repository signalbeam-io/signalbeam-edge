using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Time;

namespace SignalBeam.Shared.Infrastructure.Tests.Time;

public class FakeDateTimeProviderTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithCurrentTime()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var provider = new FakeDateTimeProvider();
        var after = DateTimeOffset.UtcNow;

        // Assert
        provider.UtcNow.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void SetTime_ShouldUpdateCurrentTime()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var targetTime = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        provider.SetTime(targetTime);

        // Assert
        provider.UtcNow.Should().Be(targetTime.ToUniversalTime());
        provider.Now.Should().Be(targetTime);
    }

    [Fact]
    public void Advance_ShouldMoveTimeForward()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        provider.SetTime(startTime);

        // Act
        provider.Advance(TimeSpan.FromHours(5));

        // Assert
        provider.UtcNow.Should().Be(startTime.AddHours(5).ToUniversalTime());
    }

    [Fact]
    public void Advance_WithNegativeDuration_ShouldMoveTimeBackward()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var startTime = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        provider.SetTime(startTime);

        // Act
        provider.Advance(TimeSpan.FromHours(-2));

        // Assert
        provider.UtcNow.Should().Be(startTime.AddHours(-2).ToUniversalTime());
    }

    [Fact]
    public void Reset_ShouldResetToCurrentTime()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var pastTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        provider.SetTime(pastTime);

        var before = DateTimeOffset.UtcNow;

        // Act
        provider.Reset();
        var after = DateTimeOffset.UtcNow;

        // Assert
        provider.UtcNow.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Today_ShouldReturnDatePartOnly()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var targetTime = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.Zero);
        provider.SetTime(targetTime);

        // Act
        var today = provider.Today;

        // Assert
        today.Hour.Should().Be(0);
        today.Minute.Should().Be(0);
        today.Second.Should().Be(0);
        today.Date.Should().Be(targetTime.Date);
    }

    [Fact]
    public void MultipleAdvances_ShouldAccumulate()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        provider.SetTime(startTime);

        // Act
        provider.Advance(TimeSpan.FromDays(1));
        provider.Advance(TimeSpan.FromHours(6));
        provider.Advance(TimeSpan.FromMinutes(30));

        // Assert
        var expected = startTime.AddDays(1).AddHours(6).AddMinutes(30);
        provider.UtcNow.Should().Be(expected.ToUniversalTime());
    }
}
