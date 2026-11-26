using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.EventHandlers;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Infrastructure.BackgroundServices;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.BackgroundServices;

public class StaleDeviceSubscriberTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly StaleDeviceSubscriber _subscriber;

    public StaleDeviceSubscriberTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_devices);
        services.AddSingleton(Substitute.For<IDeviceActivityLogRepository>());
        services.AddScoped<Microsoft.Extensions.Logging.ILogger<DeviceOfflineEventHandler>>(_ => NullLogger<DeviceOfflineEventHandler>.Instance);
        services.AddScoped<DeviceOfflineEventHandler>();
        services.AddScoped<MarkDeviceOfflineHandler>();

        _subscriber = new StaleDeviceSubscriber(
            Substitute.For<INatsConnection>(),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<StaleDeviceSubscriber>.Instance,
            Options.Create(new StaleDeviceSubscriberOptions()));
    }

    [Fact]
    public async Task ProcessAsync_ValidEvent_RunsOfflineHandler()
    {
        // Arrange: camelCase, as TelemetryProcessor publishes it.
        var deviceId = Guid.NewGuid();
        var json = $"{{\"deviceId\":\"{deviceId}\",\"lastHeartbeatTime\":\"2026-11-26T12:00:00Z\",\"detectedAt\":\"2026-11-26T12:02:00Z\"}}";

        // Act
        var handled = await _subscriber.ProcessAsync(json, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        await _devices.Received(1).GetByIdAsync(new DeviceId(deviceId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"deviceId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task ProcessAsync_UnusableEvent_IsIgnored(string? json)
    {
        // Act
        var handled = await _subscriber.ProcessAsync(json, CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
        await _devices.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default);
    }

    [Fact]
    public void Options_UseAQueueGroup()
    {
        new StaleDeviceSubscriberOptions().QueueGroup.Should().NotBeNullOrEmpty();
    }
}
