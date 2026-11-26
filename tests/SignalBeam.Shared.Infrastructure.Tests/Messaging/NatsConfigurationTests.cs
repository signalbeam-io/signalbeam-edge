using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Messaging;

namespace SignalBeam.Shared.Infrastructure.Tests.Messaging;

public class NatsConfigurationTests
{
    [Theory]
    [InlineData("tcp://localhost:4222", "nats://localhost:4222")]
    [InlineData("TCP://nats:4222", "nats://nats:4222")]
    [InlineData("nats://nats:4222", "nats://nats:4222")]
    [InlineData("tls://nats:4222", "tls://nats:4222")]
    [InlineData("localhost:4222", "nats://localhost:4222")]
    public void NormalizeUrl_ConvertsAspireEndpoints(string url, string expected)
    {
        NatsConfiguration.NormalizeUrl(url).Should().Be(expected);
    }
}
