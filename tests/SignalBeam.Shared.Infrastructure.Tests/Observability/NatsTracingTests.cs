using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using SignalBeam.ServiceDefaults;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.Shared.Infrastructure.Tests.Observability;

public class NatsTracingTests
{
    [Theory]
    // JetStream control plane: consumer fetches, API calls and their inbox replies.
    [InlineData("$JS.API.CONSUMER.MSG.NEXT.DEVICE_HEARTBEATS.telemetry-processor-heartbeats")]
    [InlineData("$JS.API.CONSUMER.CREATE.DEVICE_METRICS")]
    [InlineData("_INBOX.Wn8yZ1Cn3qcm0nTZ2fNbVX")]
    public void ShouldTrace_ControlPlaneSubject_ReturnsFalse(string subject)
    {
        // Act
        var result = NatsTracing.ShouldTrace(subject);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    // Application subjects, and acks, which sit inside a message's processing trace.
    [InlineData("signalbeam.devices.heartbeat.7e0e9111-1389-4ef3-98ff-b1e798763dad")]
    [InlineData("signalbeam.devices.events.heartbeat_received")]
    [InlineData("signalbeam.telemetry.metrics.7e0e9111-1389-4ef3-98ff-b1e798763dad")]
    [InlineData("$JS.ACK.DEVICE_HEARTBEATS.telemetry-processor-heartbeats.1.1.1.1.0")]
    [InlineData("")]
    [InlineData(null)]
    public void ShouldTrace_ApplicationSubject_ReturnsTrue(string? subject)
    {
        // Act
        var result = NatsTracing.ShouldTrace(subject);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldTrace_IsCaseSensitive_LikeNatsSubjects()
    {
        // Act
        var result = NatsTracing.ShouldTrace("$js.api.consumer.info");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ConfigureOpenTelemetry_InstallsControlPlaneFilter()
    {
        // Arrange: process-wide options; the filter is idempotent, so this is safe alongside other tests.
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.ConfigureOpenTelemetry();

        // Assert
        var filter = NatsInstrumentationOptions.Default.Filter;
        filter.Should().NotBeNull();
        filter!.Method.Name.Should().Be(nameof(NatsTracing.ShouldTrace));
    }
}
