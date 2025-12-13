using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceReportedStateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateReportedState()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedAt = DateTimeOffset.UtcNow;

        // Act
        var reportedState = DeviceReportedState.Create(deviceId, reportedAt);

        // Assert
        Assert.Equal(deviceId, reportedState.DeviceId);
        Assert.Equal(reportedAt, reportedState.ReportedAt);
        Assert.Null(reportedState.BundleId);
        Assert.Null(reportedState.Version);
        Assert.Null(reportedState.RunningContainersJson);
        Assert.Null(reportedState.ErrorMessage);
    }

    [Fact]
    public void UpdateState_WithBundleAndVersion_ShouldUpdateFields()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var newReportedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        reportedState.UpdateState(bundleId, version, newReportedAt);

        // Assert
        Assert.Equal(bundleId, reportedState.BundleId);
        Assert.Equal(version, reportedState.Version);
        Assert.Equal(newReportedAt, reportedState.ReportedAt);
    }

    [Fact]
    public void UpdateState_WithRunningContainers_ShouldStoreJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var reportedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var containersJson = "{\"containers\":[{\"id\":\"abc\",\"name\":\"nginx\",\"status\":\"running\"}]}";

        // Act
        reportedState.UpdateState(bundleId, version, reportedAt, containersJson);

        // Assert
        Assert.Equal(containersJson, reportedState.RunningContainersJson);
    }

    [Fact]
    public void UpdateState_WithErrorMessage_ShouldStoreError()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var reportedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var errorMessage = "Container failed to start";

        // Act
        reportedState.UpdateState(bundleId, version, reportedAt, errorMessage: errorMessage);

        // Assert
        Assert.Equal(errorMessage, reportedState.ErrorMessage);
    }

    [Fact]
    public void ReportError_ShouldSetErrorAndTimestamp()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var errorMessage = "Failed to download bundle";
        var errorReportedAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        reportedState.ReportError(errorMessage, errorReportedAt);

        // Assert
        Assert.Equal(errorMessage, reportedState.ErrorMessage);
        Assert.Equal(errorReportedAt, reportedState.ReportedAt);
    }

    [Fact]
    public void ReportError_ShouldNotClearBundleInfo()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        reportedState.UpdateState(bundleId, version, DateTimeOffset.UtcNow);

        // Act
        reportedState.ReportError("Something went wrong", DateTimeOffset.UtcNow.AddMinutes(1));

        // Assert
        Assert.Equal(bundleId, reportedState.BundleId);
        Assert.Equal(version, reportedState.Version);
        Assert.Equal("Something went wrong", reportedState.ErrorMessage);
    }

    [Fact]
    public void UpdateState_MultipleUpdates_ShouldKeepLatestState()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);

        var bundleId1 = BundleId.New();
        var bundleId2 = BundleId.New();

        // Act
        reportedState.UpdateState(bundleId1, BundleVersion.Parse("1.0.0"), DateTimeOffset.UtcNow.AddMinutes(1));
        reportedState.UpdateState(bundleId2, BundleVersion.Parse("2.0.0"), DateTimeOffset.UtcNow.AddMinutes(2));

        // Assert
        Assert.Equal(bundleId2, reportedState.BundleId);
        Assert.Equal(BundleVersion.Parse("2.0.0"), reportedState.Version);
    }

    [Fact]
    public void UpdateState_ShouldClearPreviousError()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var reportedState = DeviceReportedState.Create(deviceId, DateTimeOffset.UtcNow);
        reportedState.ReportError("Initial error", DateTimeOffset.UtcNow);

        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");

        // Act
        reportedState.UpdateState(bundleId, version, DateTimeOffset.UtcNow.AddMinutes(1));

        // Assert
        Assert.Null(reportedState.ErrorMessage); // Error cleared after successful update
    }
}
