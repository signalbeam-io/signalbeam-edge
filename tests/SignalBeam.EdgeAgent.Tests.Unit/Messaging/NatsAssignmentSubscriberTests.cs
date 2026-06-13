using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Messaging;

public class NatsAssignmentSubscriberTests
{
    private const string Subject = "signalbeam.bundles.assignments.test";

    private readonly NatsAssignmentSubscriber _sut;

    public NatsAssignmentSubscriberTests()
    {
        var connection = Substitute.For<INatsConnection>();
        var logger = Substitute.For<ILogger<NatsAssignmentSubscriber>>();
        _sut = new NatsAssignmentSubscriber(connection, logger);
    }

    private static byte[] Payload(object message) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

    [Fact]
    public void HandlePayload_WithValidMessage_RaisesEventWithDeserializedFields()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var assignedAt = DateTimeOffset.UtcNow;
        AssignmentReceivedEventArgs? received = null;
        _sut.OnAssignmentReceived += (_, e) => received = e;

        var payload = Payload(new
        {
            deviceId,
            bundleId = "bundle-42",
            bundleVersion = "2.1.0",
            assignedAt
        });

        // Act
        _sut.HandlePayload(Subject, payload);

        // Assert
        received.Should().NotBeNull();
        received!.DeviceId.Should().Be(deviceId);
        received.BundleId.Should().Be("bundle-42");
        received.BundleVersion.Should().Be("2.1.0");
        received.AssignedAt.Should().BeCloseTo(assignedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void HandlePayload_WithoutBundleVersion_RaisesEventWithEmptyVersion()
    {
        // Arrange — the originating domain event does not yet carry a version
        AssignmentReceivedEventArgs? received = null;
        _sut.OnAssignmentReceived += (_, e) => received = e;

        var payload = Payload(new
        {
            deviceId = Guid.NewGuid(),
            bundleId = "bundle-42",
            assignedAt = DateTimeOffset.UtcNow
        });

        // Act
        _sut.HandlePayload(Subject, payload);

        // Assert
        received.Should().NotBeNull();
        received!.BundleVersion.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void HandlePayload_WithEmptyPayload_DoesNotRaiseEvent(byte[]? data)
    {
        // Arrange
        var raised = false;
        _sut.OnAssignmentReceived += (_, _) => raised = true;

        // Act
        _sut.HandlePayload(Subject, data);

        // Assert
        raised.Should().BeFalse();
    }

    [Fact]
    public void HandlePayload_WithMalformedJson_DoesNotThrowOrRaise()
    {
        // Arrange
        var raised = false;
        _sut.OnAssignmentReceived += (_, _) => raised = true;
        var garbage = Encoding.UTF8.GetBytes("{ not valid json ");

        // Act
        var act = () => _sut.HandlePayload(Subject, garbage);

        // Assert
        act.Should().NotThrow();
        raised.Should().BeFalse();
    }
}
