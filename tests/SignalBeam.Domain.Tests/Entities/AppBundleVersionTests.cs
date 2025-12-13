using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class AppBundleVersionTests
{
    [Fact]
    public void Create_WithContainerSpecs_ShouldCreateBundleVersion()
    {
        // Arrange
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.2.3");
        var createdAt = DateTimeOffset.UtcNow;
        var containerSpecs = new[]
        {
            ContainerSpec.Create("nginx", "nginx:1.21"),
            ContainerSpec.Create("redis", "redis:7.0")
        };

        // Act
        var bundleVersion = AppBundleVersion.Create(
            bundleId,
            version,
            createdAt,
            "user@example.com",
            containerSpecs,
            releaseNotes: "Initial release");

        // Assert
        Assert.Equal(bundleId, bundleVersion.BundleId);
        Assert.Equal(version, bundleVersion.Version);
        Assert.Equal("user@example.com", bundleVersion.CreatedBy);
        Assert.Equal("Initial release", bundleVersion.ReleaseNotes);
        Assert.Equal(2, bundleVersion.ContainerSpecs.Count);
        Assert.False(bundleVersion.IsStable);
    }

    [Fact]
    public void MarkAsStable_ShouldSetIsStableToTrue()
    {
        // Arrange
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0") });

        // Act
        bundleVersion.MarkAsStable();

        // Assert
        Assert.True(bundleVersion.IsStable);
    }

    [Fact]
    public void UpdateArtifactInfo_WithValidData_ShouldUpdateFields()
    {
        // Arrange
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0") });

        var url = "https://storage.example.com/bundle.tar.gz";
        var checksum = "abc123def456";

        // Act
        bundleVersion.UpdateArtifactInfo(url, checksum);

        // Assert
        Assert.Equal(url, bundleVersion.ArtifactStorageUrl);
        Assert.Equal(checksum, bundleVersion.Checksum);
    }

    [Fact]
    public void UpdateArtifactInfo_WithEmptyUrl_ShouldThrowArgumentException()
    {
        // Arrange
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0") });

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            bundleVersion.UpdateArtifactInfo("", "checksum"));
    }
}
