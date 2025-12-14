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
        var after = DateTimeOffset.UtcNow;

        // Assert
        result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Now_ShouldReturnCurrentTime()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();
        var before = DateTimeOffset.Now;

        // Act
        var result = provider.Now;
        var after = DateTimeOffset.Now;

        // Assert
        result.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Today_ShouldReturnCurrentDateWithZeroTime()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();
        var expectedDate = DateTimeOffset.Now.Date;

        // Act
        var result = provider.Today;

        // Assert
        result.Date.Should().Be(expectedDate);
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.Second.Should().Be(0);
    }

    [Fact]
    public void MultipleCallsToUtcNow_ShouldReflectTimeProgression()
    {
        // Arrange
        var provider = new SystemDateTimeProvider();

        // Act
        var first = provider.UtcNow;
        Thread.Sleep(10); // Small delay to ensure time progression
        var second = provider.UtcNow;

        // Assert
        second.Should().BeOnOrAfter(first);
    }
}
