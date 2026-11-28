using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Services;

public class DesiredContainerMapperTests
{
    [Fact]
    public void Map_CreateBundleVersionShape_ProducesAgentSpec()
    {
        // Arrange: CreateBundleVersion stores env as an object and ports as "host:container" strings.
        var spec = ContainerSpec.Create(
            "web",
            "nginx:1.27",
            "{\"MODE\":\"prod\",\"WORKERS\":4}",
            "[\"8080:80\",\"5353:53/udp\",\"9000\"]");

        // Act
        var container = DesiredContainerMapper.Map(spec);

        // Assert
        container.Name.Should().Be("web");
        container.Image.Should().Be("nginx:1.27");
        container.Environment.Should().Equal(new Dictionary<string, string> { ["MODE"] = "prod", ["WORKERS"] = "4" });
        container.Ports.Should().Equal(new Dictionary<string, string>
        {
            ["80/tcp"] = "8080",
            ["53/udp"] = "5353",
            ["9000/tcp"] = string.Empty
        });
        container.Volumes.Should().BeNull();
    }

    [Fact]
    public void Map_UploadBundleVersionShape_ProducesAgentSpec()
    {
        // Arrange: UploadBundleVersion stores PortMapping and VolumeMount objects.
        var spec = ContainerSpec.Create(
            "sensor",
            "ghcr.io/signalbeam/temp-sensor:1.2.0",
            null,
            "[{\"containerPort\":8080,\"hostPort\":18080,\"protocol\":\"tcp\"}]",
            "[{\"name\":\"data\",\"hostPath\":\"/var/lib/sensor\",\"containerPath\":\"/data\",\"readOnly\":false}," +
            "{\"name\":\"cfg\",\"hostPath\":\"/etc/sensor\",\"containerPath\":\"/config\",\"readOnly\":true}]");

        // Act
        var container = DesiredContainerMapper.Map(spec);

        // Assert
        container.Environment.Should().BeNull();
        container.Ports.Should().Equal(new Dictionary<string, string> { ["8080/tcp"] = "18080" });
        container.Volumes.Should().Equal(new Dictionary<string, string>
        {
            ["/var/lib/sensor"] = "/data",
            ["/etc/sensor"] = "/config:ro"
        });
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("42")]
    [InlineData("[\"abc\", {\"containerPort\": \"x\"}]")]
    public void MapPorts_UnreadableEntries_AreLeftOut(string json)
    {
        DesiredContainerMapper.MapPorts(json).Should().BeNullOrEmpty();
    }
}
