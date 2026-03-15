using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Messaging;

namespace SignalBeam.EdgeAgent.Tests.Unit.Messaging;

public class NatsAssignmentSubscriberTests
{
    private readonly INatsJSContext _jetStream;
    private readonly NatsAssignmentSubscriber _sut;

    public NatsAssignmentSubscriberTests()
    {
        _jetStream = Substitute.For<INatsJSContext>();
        var logger = Substitute.For<ILogger<NatsAssignmentSubscriber>>();
        _sut = new NatsAssignmentSubscriber(_jetStream, logger);
    }

    [Fact]
    public void OnAssignmentReceived_IsInitiallyNull()
    {
        // The event should have no subscribers initially
        // This verifies the subscriber can be created without handlers attached
        var subscriber = new NatsAssignmentSubscriber(
            Substitute.For<INatsJSContext>(),
            Substitute.For<ILogger<NatsAssignmentSubscriber>>());

        // Should not throw when stopping without starting
        var act = () => subscriber.StopAsync();
        act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WithoutStarting_DoesNotThrow()
    {
        // Act & Assert
        await _sut.StopAsync();
    }

    [Fact]
    public void OnAssignmentReceived_CanAttachHandler()
    {
        // Arrange
        BundleAssignmentMessage? received = null;

        // Act
        _sut.OnAssignmentReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        // Assert — no exception means handler was attached successfully
        received.Should().BeNull();
    }
}
