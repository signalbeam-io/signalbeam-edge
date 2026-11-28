using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Tests.Unit.Services;

public class ManagedContainerLabelsTests
{
    [Fact]
    public void For_LabelsTheContainerWithItsBundleAndImage()
    {
        var labels = ManagedContainerLabels.For(new ContainerSpec("web", "nginx:1.27"), new ContainerOwner("bundle-1", "1.2.0"));

        labels.Should().Equal(new Dictionary<string, string>
        {
            [ManagedContainerLabels.Managed] = "true",
            [ManagedContainerLabels.Image] = "nginx:1.27",
            [ManagedContainerLabels.BundleId] = "bundle-1",
            [ManagedContainerLabels.BundleVersion] = "1.2.0"
        });
        ManagedContainerLabels.IsManaged(labels).Should().BeTrue();
    }

    [Fact]
    public void For_WithoutBundle_StillMarksTheContainerManaged()
    {
        var labels = ManagedContainerLabels.For(new ContainerSpec("web", "nginx:1.27"), new ContainerOwner(null, null));

        labels.Should().NotContainKey(ManagedContainerLabels.BundleId);
        ManagedContainerLabels.IsManaged(labels).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("TRUE")] // Docker's label filter is exact, so IsManaged is too
    [InlineData("")]
    public void IsManaged_WithoutTheTrueLabel_IsFalse(string? value)
    {
        var labels = value is null
            ? new Dictionary<string, string> { ["com.docker.compose.project"] = "home" }
            : new Dictionary<string, string> { [ManagedContainerLabels.Managed] = value };

        ManagedContainerLabels.IsManaged(labels).Should().BeFalse();
    }

    [Fact]
    public void IsManaged_NoLabels_IsFalse()
    {
        ManagedContainerLabels.IsManaged(null).Should().BeFalse();
    }
}
