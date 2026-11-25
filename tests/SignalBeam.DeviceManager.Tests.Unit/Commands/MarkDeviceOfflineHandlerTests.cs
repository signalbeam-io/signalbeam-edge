using Microsoft.Extensions.Logging.Abstractions;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.EventHandlers;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Unit.Commands;

public class MarkDeviceOfflineHandlerTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IDeviceActivityLogRepository _activityLog = Substitute.For<IDeviceActivityLogRepository>();
    private readonly MarkDeviceOfflineHandler _handler;
    private readonly DeviceId _deviceId = new(Guid.NewGuid());
    private readonly DateTimeOffset _now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    public MarkDeviceOfflineHandlerTests()
    {
        _handler = new MarkDeviceOfflineHandler(
            _devices,
            new DeviceOfflineEventHandler(NullLogger<DeviceOfflineEventHandler>.Instance, _activityLog));
    }

    [Fact]
    public async Task Handle_StaleOnlineDevice_MarksOfflineAndLogsActivity()
    {
        // Arrange
        var device = OnlineDevice(lastSeen: _now.AddMinutes(-5));

        // Act
        var result = await _handler.Handle(Stale(lastHeartbeat: _now.AddMinutes(-2)), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MarkedOffline.Should().BeTrue();
        device.Status.Should().Be(DeviceStatus.Offline);
        device.DomainEvents.Should().BeEmpty();
        await _devices.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _activityLog.Received(1).AddAsync(
            Arg.Is<DeviceActivityLog>(l => l.ActivityType == "DeviceOffline"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeviceSeenOverHttpSinceStaleCutoff_StaysOnline()
    {
        // Arrange: NATS heartbeats stopped, but DeviceManager got an HTTP heartbeat 20s ago.
        var device = OnlineDevice(lastSeen: _now.AddSeconds(-20));

        // Act
        var result = await _handler.Handle(Stale(lastHeartbeat: _now.AddMinutes(-2)), CancellationToken.None);

        // Assert
        result.Value!.MarkedOffline.Should().BeFalse();
        device.Status.Should().Be(DeviceStatus.Online);
        await _devices.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyOffline_IsNoOp()
    {
        // Arrange: the monitor re-publishes the event every cycle while the device stays stale.
        var device = OnlineDevice(lastSeen: _now.AddMinutes(-10));
        device.MarkAsOffline(_now.AddMinutes(-7));
        device.ClearDomainEvents();

        // Act
        var result = await _handler.Handle(Stale(lastHeartbeat: _now.AddMinutes(-2)), CancellationToken.None);

        // Assert
        result.Value!.MarkedOffline.Should().BeFalse();
        await _devices.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _activityLog.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_UnknownDevice_ReturnsNotFound()
    {
        // Act
        var result = await _handler.Handle(Stale(lastHeartbeat: _now.AddMinutes(-2)), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_HeartbeatAfterOffline_BringsDeviceBackOnline()
    {
        // Arrange
        var device = OnlineDevice(lastSeen: _now.AddMinutes(-5));
        await _handler.Handle(Stale(lastHeartbeat: _now.AddMinutes(-2)), CancellationToken.None);

        // Act: flapping connectivity, the device comes back.
        device.RecordHeartbeat(_now.AddMinutes(1));

        // Assert
        device.Status.Should().Be(DeviceStatus.Online);
    }

    private Device OnlineDevice(DateTimeOffset lastSeen)
    {
        var device = Device.Register(_deviceId, new TenantId(Guid.NewGuid()), "sim-lab-rpi-001", _now.AddDays(-1));
        device.RecordHeartbeat(lastSeen);
        device.ClearDomainEvents();
        _devices.GetByIdAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(device);
        return device;
    }

    private MarkDeviceOfflineCommand Stale(DateTimeOffset lastHeartbeat) =>
        new(_deviceId.Value, lastHeartbeat, _now);
}
