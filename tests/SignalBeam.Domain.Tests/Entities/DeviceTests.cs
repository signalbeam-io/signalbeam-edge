using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceTests
{
    [Fact]
    public void Register_WithValidData_ShouldCreateDevice()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var tenantId = TenantId.New();
        var name = "Test Device";
        var registeredAt = DateTimeOffset.UtcNow;

        // Act
        var device = Device.Register(deviceId, tenantId, name, registeredAt);

        // Assert
        Assert.Equal(deviceId, device.Id);
        Assert.Equal(tenantId, device.TenantId);
        Assert.Equal(name, device.Name);
        Assert.Equal(DeviceStatus.Registered, device.Status);
        Assert.Equal(registeredAt, device.RegisteredAt);
        Assert.Null(device.LastSeenAt);
    }

    [Fact]
    public void Register_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var tenantId = TenantId.New();
        var registeredAt = DateTimeOffset.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Device.Register(deviceId, tenantId, "", registeredAt));
    }

    [Fact]
    public void Register_ShouldRaiseDeviceRegisteredEvent()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var tenantId = TenantId.New();
        var name = "Test Device";
        var registeredAt = DateTimeOffset.UtcNow;

        // Act
        var device = Device.Register(deviceId, tenantId, name, registeredAt);

        // Assert
        Assert.Single(device.DomainEvents);
        var domainEvent = device.DomainEvents.First();
        Assert.IsType<DeviceRegisteredEvent>(domainEvent);

        var registeredEvent = (DeviceRegisteredEvent)domainEvent;
        Assert.Equal(deviceId, registeredEvent.DeviceId);
        Assert.Equal(tenantId, registeredEvent.TenantId);
        Assert.Equal(name, registeredEvent.DeviceName);
    }

    [Fact]
    public void RecordHeartbeat_ShouldUpdateLastSeenAndSetOnline()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var heartbeatTime = DateTimeOffset.UtcNow;

        // Act
        device.RecordHeartbeat(heartbeatTime);

        // Assert
        Assert.Equal(DeviceStatus.Online, device.Status);
        Assert.Equal(heartbeatTime, device.LastSeenAt);
    }

    [Fact]
    public void RecordHeartbeat_WhenOffline_ShouldRaiseDeviceOnlineEvent()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        device.MarkAsOffline(DateTimeOffset.UtcNow);
        device.ClearDomainEvents(); // Clear previous events

        var heartbeatTime = DateTimeOffset.UtcNow;

        // Act
        device.RecordHeartbeat(heartbeatTime);

        // Assert
        Assert.Contains(device.DomainEvents, e => e is DeviceOnlineEvent);
    }

    [Fact]
    public void MarkAsOffline_ShouldChangeStatusToOffline()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        device.RecordHeartbeat(DateTimeOffset.UtcNow); // Make it online first
        device.ClearDomainEvents();

        // Act
        device.MarkAsOffline(DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(DeviceStatus.Offline, device.Status);
        Assert.Contains(device.DomainEvents, e => e is DeviceOfflineEvent);
    }

    [Fact]
    public void AssignBundle_ShouldSetAssignedBundleAndStatus()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var assignedAt = DateTimeOffset.UtcNow;

        // Act
        device.AssignBundle(bundleId, assignedAt);

        // Assert
        Assert.Equal(bundleId, device.AssignedBundleId);
        Assert.Equal(BundleDeploymentStatus.Pending, device.BundleDeploymentStatus);
        Assert.Contains(device.DomainEvents, e => e is BundleAssignedEvent);
    }

    [Fact]
    public void UpdateBundleDeploymentStatus_ToCompleted_ShouldSetOnlineStatus()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        device.AssignBundle(bundleId, DateTimeOffset.UtcNow);
        device.ClearDomainEvents();

        // Act
        device.UpdateBundleDeploymentStatus(BundleDeploymentStatus.Completed, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(DeviceStatus.Online, device.Status);
        Assert.Equal(BundleDeploymentStatus.Completed, device.BundleDeploymentStatus);
        Assert.Contains(device.DomainEvents, e => e is BundleUpdateCompletedEvent);
    }

    [Fact]
    public void UpdateBundleDeploymentStatus_ToFailed_ShouldSetErrorStatus()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        device.AssignBundle(bundleId, DateTimeOffset.UtcNow);
        device.ClearDomainEvents();

        // Act
        device.UpdateBundleDeploymentStatus(BundleDeploymentStatus.Failed, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(DeviceStatus.Error, device.Status);
        Assert.Equal(BundleDeploymentStatus.Failed, device.BundleDeploymentStatus);
        Assert.Contains(device.DomainEvents, e => e is BundleUpdateFailedEvent);
    }

    [Fact]
    public void AddTag_ShouldAddTagToDevice()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        // Act
        device.AddTag("production");
        device.AddTag("rpi4");

        // Assert
        Assert.Equal(2, device.Tags.Count);
        Assert.Contains("production", device.Tags);
        Assert.Contains("rpi4", device.Tags);
    }

    [Fact]
    public void AddTag_WithDuplicate_ShouldNotAddAgain()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        // Act
        device.AddTag("production");
        device.AddTag("PRODUCTION"); // Different case

        // Assert
        Assert.Single(device.Tags);
    }

    [Fact]
    public void RemoveTag_ShouldRemoveTagFromDevice()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        device.AddTag("production");
        device.AddTag("rpi4");

        // Act
        device.RemoveTag("production");

        // Assert
        Assert.Single(device.Tags);
        Assert.Contains("rpi4", device.Tags);
        Assert.DoesNotContain("production", device.Tags);
    }

    [Fact]
    public void AssignToGroup_ShouldSetDeviceGroupId()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var groupId = DeviceGroupId.New();

        // Act
        device.AssignToGroup(groupId);

        // Assert
        Assert.Equal(groupId, device.DeviceGroupId);
    }

    [Fact]
    public void RemoveFromGroup_ShouldClearDeviceGroupId()
    {
        // Arrange
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "Test Device",
            DateTimeOffset.UtcNow);

        var groupId = DeviceGroupId.New();
        device.AssignToGroup(groupId);

        // Act
        device.RemoveFromGroup();

        // Assert
        Assert.Null(device.DeviceGroupId);
    }
}
