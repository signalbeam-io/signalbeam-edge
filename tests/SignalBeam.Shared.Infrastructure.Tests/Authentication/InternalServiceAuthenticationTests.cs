using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

public class InternalServiceAuthenticationTests
{
    [Theory]
    [InlineData("s3cret-key", "s3cret-key", true)]
    [InlineData("s3cret-key", "s3cret-kez", false)]
    [InlineData("s3cret-key", "s3cret", false)]
    [InlineData("s3cret-key", "", false)]
    [InlineData("s3cret-key", null, false)]
    // No key configured: every call is refused, even one that sends an empty key.
    [InlineData(null, "", false)]
    [InlineData("", "", false)]
    [InlineData(null, "anything", false)]
    public void IsValid_ComparesKeysAndFailsClosed(string? expected, string? presented, bool valid)
    {
        // Act
        var result = InternalServiceAuthentication.IsValid(expected, presented);

        // Assert
        result.Should().Be(valid);
    }
}
