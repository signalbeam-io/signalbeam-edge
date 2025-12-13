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

    [Fact]
    public void UpdateArtifactInfo_WithEmptyChecksum_ShouldThrowArgumentException()
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
            bundleVersion.UpdateArtifactInfo("https://storage.example.com/bundle.tar.gz", ""));
    }

    [Fact]
    public void MarkAsUnstable_ShouldSetIsStableToFalse()
    {
        // Arrange
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0") });

        bundleVersion.MarkAsStable();

        // Act
        bundleVersion.MarkAsUnstable();

        // Assert
        Assert.False(bundleVersion.IsStable);
    }

    [Fact]
    public void Create_WithMultipleContainerSpecs_ShouldStoreAll()
    {
        // Arrange
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("2.0.0");
        var createdAt = DateTimeOffset.UtcNow;
        var containerSpecs = new[]
        {
            ContainerSpec.Create("nginx", "nginx:1.21"),
            ContainerSpec.Create("redis", "redis:7.0"),
            ContainerSpec.Create("postgres", "postgres:14")
        };

        // Act
        var bundleVersion = AppBundleVersion.Create(
            bundleId,
            version,
            createdAt,
            "admin@example.com",
            containerSpecs);

        // Assert
        Assert.Equal(3, bundleVersion.ContainerSpecs.Count);
        Assert.Contains(bundleVersion.ContainerSpecs, c => c.Name == "nginx");
        Assert.Contains(bundleVersion.ContainerSpecs, c => c.Name == "redis");
        Assert.Contains(bundleVersion.ContainerSpecs, c => c.Name == "postgres");
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ShouldThrowArgumentException()
    {
        // Arrange
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var containerSpecs = new[] { ContainerSpec.Create("app", "app:1.0.0") };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            AppBundleVersion.Create(
                bundleId,
                version,
                DateTimeOffset.UtcNow,
                "",
                containerSpecs));
    }

    [Fact]
    public void Create_WithWhitespaceCreatedBy_ShouldThrowArgumentException()
    {
        // Arrange
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.0.0");
        var containerSpecs = new[] { ContainerSpec.Create("app", "app:1.0.0") };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            AppBundleVersion.Create(
                bundleId,
                version,
                DateTimeOffset.UtcNow,
                "   ",
                containerSpecs));
    }

    [Fact]
    public void Create_WithAllOptionalParameters_ShouldStoreAll()
    {
        // Arrange
        var bundleId = BundleId.New();
        var version = BundleVersion.Parse("1.5.0");
        var createdAt = DateTimeOffset.UtcNow;
        var containerSpecs = new[] { ContainerSpec.Create("app", "app:1.5.0") };
        var releaseNotes = "Bug fixes and performance improvements";
        var artifactUrl = "https://storage.example.com/bundles/1.5.0.tar.gz";
        var checksum = "sha256:abc123def456";

        // Act
        var bundleVersion = AppBundleVersion.Create(
            bundleId,
            version,
            createdAt,
            "ci-system@example.com",
            containerSpecs,
            releaseNotes,
            artifactUrl,
            checksum);

        // Assert
        Assert.Equal(releaseNotes, bundleVersion.ReleaseNotes);
        Assert.Equal(artifactUrl, bundleVersion.ArtifactStorageUrl);
        Assert.Equal(checksum, bundleVersion.Checksum);
    }

    [Fact]
    public void Create_DefaultIsStable_ShouldBeFalse()
    {
        // Arrange & Act
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0-beta"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0-beta") });

        // Assert
        Assert.False(bundleVersion.IsStable);
    }

    [Fact]
    public void UpdateArtifactInfo_MultipleTimes_ShouldKeepLatest()
    {
        // Arrange
        var bundleVersion = AppBundleVersion.Create(
            BundleId.New(),
            BundleVersion.Parse("1.0.0"),
            DateTimeOffset.UtcNow,
            "user@example.com",
            new[] { ContainerSpec.Create("app", "app:1.0.0") });

        // Act
        bundleVersion.UpdateArtifactInfo("https://storage1.example.com/bundle.tar.gz", "checksum1");
        bundleVersion.UpdateArtifactInfo("https://storage2.example.com/bundle.tar.gz", "checksum2");

        // Assert
        Assert.Equal("https://storage2.example.com/bundle.tar.gz", bundleVersion.ArtifactStorageUrl);
        Assert.Equal("checksum2", bundleVersion.Checksum);
    }
}
