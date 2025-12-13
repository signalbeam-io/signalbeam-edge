using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceEventTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateEvent()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var eventType = "DeviceRegistered";
        var severity = EventSeverity.Information;
        var message = "Device successfully registered";

        // Act
        var deviceEvent = DeviceEvent.Create(
            deviceId,
            timestamp,
            eventType,
            severity,
            message);

        // Assert
        Assert.Equal(deviceId, deviceEvent.DeviceId);
        Assert.Equal(timestamp, deviceEvent.Timestamp);
        Assert.Equal(eventType, deviceEvent.EventType);
        Assert.Equal(severity, deviceEvent.Severity);
        Assert.Equal(message, deviceEvent.Message);
        Assert.Null(deviceEvent.DataJson);
        Assert.Null(deviceEvent.TriggeredBy);
    }

    [Fact]
    public void Create_WithDataJson_ShouldStoreJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var dataJson = "{\"bundleId\":\"abc-123\",\"version\":\"1.0.0\"}";

        // Act
        var deviceEvent = DeviceEvent.Create(
            deviceId,
            timestamp,
            "BundleAssigned",
            EventSeverity.Information,
            "Bundle assigned to device",
            dataJson: dataJson);

        // Assert
        Assert.Equal(dataJson, deviceEvent.DataJson);
    }

    [Fact]
    public void Create_WithTriggeredBy_ShouldStoreTriggeredBy()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var triggeredBy = "admin@example.com";

        // Act
        var deviceEvent = DeviceEvent.Create(
            deviceId,
            timestamp,
            "ConfigurationChanged",
            EventSeverity.Information,
            "Device configuration updated",
            triggeredBy: triggeredBy);

        // Assert
        Assert.Equal(triggeredBy, deviceEvent.TriggeredBy);
    }

    [Fact]
    public void Create_InformationSeverity_ShouldCreateEvent()
    {
        // Arrange & Act
        var deviceEvent = DeviceEvent.Create(
            DeviceId.New(),
            DateTimeOffset.UtcNow,
            "DeviceOnline",
            EventSeverity.Information,
            "Device came online");

        // Assert
        Assert.Equal(EventSeverity.Information, deviceEvent.Severity);
    }

    [Fact]
    public void Create_WarningSeverity_ShouldCreateEvent()
    {
        // Arrange & Act
        var deviceEvent = DeviceEvent.Create(
            DeviceId.New(),
            DateTimeOffset.UtcNow,
            "HighCpuUsage",
            EventSeverity.Warning,
            "CPU usage above 80%");

        // Assert
        Assert.Equal(EventSeverity.Warning, deviceEvent.Severity);
    }

    [Fact]
    public void Create_ErrorSeverity_ShouldCreateEvent()
    {
        // Arrange & Act
        var deviceEvent = DeviceEvent.Create(
            DeviceId.New(),
            DateTimeOffset.UtcNow,
            "BundleDeploymentFailed",
            EventSeverity.Error,
            "Failed to deploy bundle");

        // Assert
        Assert.Equal(EventSeverity.Error, deviceEvent.Severity);
    }

    [Fact]
    public void Create_CriticalSeverity_ShouldCreateEvent()
    {
        // Arrange & Act
        var deviceEvent = DeviceEvent.Create(
            DeviceId.New(),
            DateTimeOffset.UtcNow,
            "DeviceUnresponsive",
            EventSeverity.Critical,
            "Device not responding to heartbeat");

        // Assert
        Assert.Equal(EventSeverity.Critical, deviceEvent.Severity);
    }

    [Fact]
    public void Create_WithEmptyEventType_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceEvent.Create(
                deviceId,
                DateTimeOffset.UtcNow,
                "",
                EventSeverity.Information,
                "Test message"));
    }

    [Fact]
    public void Create_WithEmptyMessage_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceEvent.Create(
                deviceId,
                DateTimeOffset.UtcNow,
                "TestEvent",
                EventSeverity.Information,
                ""));
    }

    [Fact]
    public void Create_WithWhitespaceEventType_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceEvent.Create(
                deviceId,
                DateTimeOffset.UtcNow,
                "   ",
                EventSeverity.Information,
                "Test message"));
    }

    [Fact]
    public void Create_WithWhitespaceMessage_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceEvent.Create(
                deviceId,
                DateTimeOffset.UtcNow,
                "TestEvent",
                EventSeverity.Information,
                "   "));
    }

    [Fact]
    public void Create_MultipleEvents_ShouldHaveDifferentIds()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var event1 = DeviceEvent.Create(deviceId, timestamp, "Event1", EventSeverity.Information, "Message 1");
        var event2 = DeviceEvent.Create(deviceId, timestamp, "Event2", EventSeverity.Information, "Message 2");
        var event3 = DeviceEvent.Create(deviceId, timestamp, "Event3", EventSeverity.Warning, "Message 3");

        // Assert
        Assert.NotEqual(event1.Id, event2.Id);
        Assert.NotEqual(event2.Id, event3.Id);
        Assert.NotEqual(event1.Id, event3.Id);
    }

    [Fact]
    public void EventSeverity_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)EventSeverity.Information);
        Assert.Equal(1, (int)EventSeverity.Warning);
        Assert.Equal(2, (int)EventSeverity.Error);
        Assert.Equal(3, (int)EventSeverity.Critical);
    }
}
