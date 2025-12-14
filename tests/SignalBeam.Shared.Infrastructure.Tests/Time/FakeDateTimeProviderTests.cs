using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Time;

namespace SignalBeam.Shared.Infrastructure.Tests.Time;

public class FakeDateTimeProviderTests
{
    [Fact]
    public void Constructor_WithNoArguments_ShouldUseCurrentTime()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var provider = new FakeDateTimeProvider();

        // Assert
        var after = DateTimeOffset.UtcNow;
        provider.UtcNow.Should().BeOnOrAfter(before);
        provider.UtcNow.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithSpecificTime_ShouldUseProvidedTime()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2024, 12, 14, 10, 30, 0, TimeSpan.Zero);

        // Act
        var provider = new FakeDateTimeProvider(specificTime);

        // Assert
        provider.UtcNow.Should().Be(specificTime.ToUniversalTime());
        provider.Now.Should().Be(specificTime);
    }

    [Fact]
    public void SetTime_ShouldUpdateCurrentTime()
    {
        // Arrange
        var provider = new FakeDateTimeProvider();
        var newTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        provider.SetTime(newTime);

        // Assert
        provider.UtcNow.Should().Be(newTime.ToUniversalTime());
    }

    [Fact]
    public void Advance_ShouldAddDurationToCurrentTime()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 12, 14, 10, 0, 0, TimeSpan.Zero);
        var provider = new FakeDateTimeProvider(initialTime);
        var duration = TimeSpan.FromHours(2);

        // Act
        provider.Advance(duration);

        // Assert
        provider.UtcNow.Should().Be(initialTime.Add(duration).ToUniversalTime());
    }

    [Fact]
    public void Advance_MultipleNimes_ShouldAccumulateDuration()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 12, 14, 10, 0, 0, TimeSpan.Zero);
        var provider = new FakeDateTimeProvider(initialTime);

        // Act
        provider.Advance(TimeSpan.FromMinutes(30));
        provider.Advance(TimeSpan.FromMinutes(45));
        provider.Advance(TimeSpan.FromHours(1));

        // Assert
        var expectedTime = initialTime
            .AddMinutes(30)
            .AddMinutes(45)
            .AddHours(1);
        provider.UtcNow.Should().Be(expectedTime.ToUniversalTime());
    }

    [Fact]
    public void Reset_ShouldSetTimeToCurrentSystemTime()
    {
        // Arrange
        var pastTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var provider = new FakeDateTimeProvider(pastTime);
        var before = DateTimeOffset.UtcNow;

        // Act
        provider.Reset();

        // Assert
        var after = DateTimeOffset.UtcNow;
        provider.UtcNow.Should().BeOnOrAfter(before);
        provider.UtcNow.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Today_ShouldReturnDatePortionOnly()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2024, 12, 14, 15, 30, 45, TimeSpan.Zero);
        var provider = new FakeDateTimeProvider(specificTime);

        // Act
        var today = provider.Today;

        // Assert
        today.Should().Be(specificTime.Date);
        today.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UtcNow_ShouldAlwaysReturnUtcTime()
    {
        // Arrange
        var localTime = new DateTimeOffset(2024, 12, 14, 10, 0, 0, TimeSpan.FromHours(5));
        var provider = new FakeDateTimeProvider(localTime);

        // Act
        var utcTime = provider.UtcNow;

        // Assert
        utcTime.Offset.Should().Be(TimeSpan.Zero);
        utcTime.Should().Be(localTime.ToUniversalTime());
    }
}
