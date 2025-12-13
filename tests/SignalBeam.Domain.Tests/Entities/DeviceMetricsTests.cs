using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class DeviceMetricsTests
{
    [Fact]
    public void Create_WithBasicMetric_ShouldCreateMetrics()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var metricName = "cpu.usage";
        var value = 45.5;

        // Act
        var metrics = DeviceMetrics.Create(deviceId, timestamp, metricName, value);

        // Assert
        Assert.Equal(deviceId, metrics.DeviceId);
        Assert.Equal(timestamp, metrics.Timestamp);
        Assert.Equal(metricName, metrics.MetricName);
        Assert.Equal(value, metrics.Value);
        Assert.Null(metrics.Unit);
        Assert.Null(metrics.TagsJson);
    }

    [Fact]
    public void Create_WithUnit_ShouldStoreUnit()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            timestamp,
            "temperature",
            65.5,
            unit: "celsius");

        // Assert
        Assert.Equal("celsius", metrics.Unit);
    }

    [Fact]
    public void Create_WithTags_ShouldStoreTagsJson()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;
        var tagsJson = "{\"container\":\"nginx\",\"interface\":\"eth0\"}";

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            timestamp,
            "network.rx.bytes",
            1024000,
            unit: "bytes",
            tagsJson: tagsJson);

        // Assert
        Assert.Equal(tagsJson, metrics.TagsJson);
    }

    [Fact]
    public void Create_CpuMetric_ShouldStoreCorrectly()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "container.cpu.usage",
            75.2,
            unit: "percent",
            tagsJson: "{\"container\":\"nginx\"}");

        // Assert
        Assert.Equal("container.cpu.usage", metrics.MetricName);
        Assert.Equal(75.2, metrics.Value);
        Assert.Equal("percent", metrics.Unit);
    }

    [Fact]
    public void Create_MemoryMetric_ShouldStoreCorrectly()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "container.memory.bytes",
            524288000,
            unit: "bytes",
            tagsJson: "{\"container\":\"redis\"}");

        // Assert
        Assert.Equal("container.memory.bytes", metrics.MetricName);
        Assert.Equal(524288000, metrics.Value);
    }

    [Fact]
    public void Create_NetworkMetric_ShouldStoreCorrectly()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "network.tx.bytes",
            2048576,
            unit: "bytes",
            tagsJson: "{\"interface\":\"eth0\"}");

        // Assert
        Assert.Equal("network.tx.bytes", metrics.MetricName);
        Assert.Equal(2048576, metrics.Value);
    }

    [Fact]
    public void Create_WithEmptyMetricName_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceMetrics.Create(deviceId, DateTimeOffset.UtcNow, "", 100));
    }

    [Fact]
    public void Create_WithWhitespaceMetricName_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DeviceMetrics.Create(deviceId, DateTimeOffset.UtcNow, "   ", 100));
    }

    [Fact]
    public void Create_WithZeroValue_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "disk.errors",
            0);

        // Assert
        Assert.Equal(0, metrics.Value);
    }

    [Fact]
    public void Create_WithNegativeValue_ShouldStore()
    {
        // Arrange
        var deviceId = DeviceId.New();

        // Act
        var metrics = DeviceMetrics.Create(
            deviceId,
            DateTimeOffset.UtcNow,
            "temperature.delta",
            -5.3,
            unit: "celsius");

        // Assert
        Assert.Equal(-5.3, metrics.Value);
    }

    [Fact]
    public void Create_MultipleMetricsForSameDevice_ShouldCreateSeparateEntities()
    {
        // Arrange
        var deviceId = DeviceId.New();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var metric1 = DeviceMetrics.Create(deviceId, timestamp, "cpu.usage", 50.0);
        var metric2 = DeviceMetrics.Create(deviceId, timestamp, "memory.usage", 60.0);
        var metric3 = DeviceMetrics.Create(deviceId, timestamp, "disk.usage", 70.0);

        // Assert
        Assert.NotEqual(metric1.Id, metric2.Id);
        Assert.NotEqual(metric2.Id, metric3.Id);
        Assert.Equal(deviceId, metric1.DeviceId);
        Assert.Equal(deviceId, metric2.DeviceId);
        Assert.Equal(deviceId, metric3.DeviceId);
    }
}
