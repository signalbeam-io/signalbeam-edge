using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceDesiredStateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateDesiredState()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;

        // Act
        var desiredState = DeviceDesiredState.Create(deviceId, bundleId, version, setAt);

        // Assert
        Assert.Equal(deviceId, desiredState.DeviceId);
        Assert.Equal(bundleId, desiredState.BundleId);
        Assert.Equal(version, desiredState.Version);
        Assert.Equal(setAt, desiredState.SetAt);
        Assert.Equal(setAt, desiredState.UpdatedAt);
        Assert.Null(desiredState.ContainerSpecsJson);
    }

    [Fact]
    public void Create_WithContainerSpecs_ShouldStoreJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;
        var containerSpecs = "{\"containers\":[{\"name\":\"nginx\",\"image\":\"nginx:1.21\"}]}";

        // Act
        var desiredState = DeviceDesiredState.Create(
            deviceId,
            bundleId,
            version,
            setAt,
            containerSpecs);

        // Assert
        Assert.Equal(containerSpecs, desiredState.ContainerSpecsJson);
    }

    [Fact]
    public void UpdateVersion_ShouldUpdateVersionAndTimestamp()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;
        var desiredState = DeviceDesiredState.Create(deviceId, bundleId, version, setAt);

        var newVersion = BundleVersion.Parse("1.1.0");
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        desiredState.UpdateVersion(newVersion, updatedAt);

        // Assert
        Assert.Equal(newVersion, desiredState.Version);
        Assert.Equal(updatedAt, desiredState.UpdatedAt);
        Assert.Equal(setAt, desiredState.SetAt); // SetAt should not change
    }

    [Fact]
    public void UpdateVersion_WithContainerSpecs_ShouldUpdateJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;
        var desiredState = DeviceDesiredState.Create(deviceId, bundleId, version, setAt);

        var newVersion = BundleVersion.Parse("1.1.0");
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var newContainerSpecs = "{\"containers\":[{\"name\":\"redis\",\"image\":\"redis:7.0\"}]}";

        // Act
        desiredState.UpdateVersion(newVersion, updatedAt, newContainerSpecs);

        // Assert
        Assert.Equal(newVersion, desiredState.Version);
        Assert.Equal(newContainerSpecs, desiredState.ContainerSpecsJson);
    }

    [Fact]
    public void UpdateVersion_ShouldNotChangeSetAt()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;
        var desiredState = DeviceDesiredState.Create(deviceId, bundleId, version, setAt);

        var originalSetAt = desiredState.SetAt;

        // Act
        desiredState.UpdateVersion(BundleVersion.Parse("1.1.0"), DateTimeOffset.UtcNow.AddMinutes(10));

        // Assert
        Assert.Equal(originalSetAt, desiredState.SetAt);
    }

    [Fact]
    public void UpdateVersion_MultipleUpdates_ShouldKeepLatestVersion()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var setAt = DateTimeOffset.UtcNow;
        var desiredState = DeviceDesiredState.Create(deviceId, bundleId, version, setAt);

        // Act
        desiredState.UpdateVersion(BundleVersion.Parse("1.1.0"), DateTimeOffset.UtcNow.AddMinutes(1));
        desiredState.UpdateVersion(BundleVersion.Parse("1.2.0"), DateTimeOffset.UtcNow.AddMinutes(2));
        desiredState.UpdateVersion(BundleVersion.Parse("2.0.0"), DateTimeOffset.UtcNow.AddMinutes(3));

        // Assert
        Assert.Equal(BundleVersion.Parse("2.0.0"), desiredState.Version);
    }
}
