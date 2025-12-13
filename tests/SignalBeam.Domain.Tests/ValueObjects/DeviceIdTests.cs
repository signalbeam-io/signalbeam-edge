using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.ValueObjects;

public class DeviceIdTests
{
    [Fact]
    public void New_ShouldCreateValidDeviceId()
    {
        // Act
        var deviceId = DeviceId.New();

        // Assert
        Assert.NotEqual(Guid.Empty, deviceId.Value);
    }

    [Fact]
    public void Constructor_WithEmptyGuid_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DeviceId(Guid.Empty));
    }

    [Fact]
    public void Constructor_WithValidGuid_ShouldCreateDeviceId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var deviceId = new DeviceId(guid);

        // Assert
        Assert.Equal(guid, deviceId.Value);
    }

    [Fact]
    public void Parse_WithValidGuid_ShouldReturnDeviceId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var deviceId = DeviceId.Parse(guidString);

        // Assert
        Assert.Equal(guid, deviceId.Value);
    }

    [Fact]
    public void Parse_WithInvalidString_ShouldThrowFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => DeviceId.Parse("invalid-guid"));
    }

    [Fact]
    public void TryParse_WithValidGuid_ShouldReturnTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = DeviceId.TryParse(guidString, out var deviceId);

        // Assert
        Assert.True(result);
        Assert.Equal(guid, deviceId.Value);
    }

    [Fact]
    public void TryParse_WithInvalidString_ShouldReturnFalse()
    {
        // Act
        var result = DeviceId.TryParse("invalid-guid", out var deviceId);

        // Assert
        Assert.False(result);
        Assert.Equal(default, deviceId);
    }

    [Fact]
    public void Equality_WithSameValue_ShouldBeEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var deviceId1 = new DeviceId(guid);
        var deviceId2 = new DeviceId(guid);

        // Act & Assert
        Assert.Equal(deviceId1, deviceId2);
        Assert.True(deviceId1 == deviceId2);
        Assert.False(deviceId1 != deviceId2);
    }

    [Fact]
    public void Equality_WithDifferentValue_ShouldNotBeEqual()
    {
        // Arrange
        var deviceId1 = DeviceId.New();
        var deviceId2 = DeviceId.New();

        // Act & Assert
        Assert.NotEqual(deviceId1, deviceId2);
        Assert.False(deviceId1 == deviceId2);
        Assert.True(deviceId1 != deviceId2);
    }

    [Fact]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var deviceId = new DeviceId(guid);

        // Act
        var result = deviceId.ToString();

        // Assert
        Assert.Equal(guid.ToString(), result);
    }

    [Fact]
    public void ImplicitConversion_ToGuid_ShouldWork()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var deviceId = new DeviceId(guid);

        // Act
        Guid convertedGuid = deviceId;

        // Assert
        Assert.Equal(guid, convertedGuid);
    }
}
