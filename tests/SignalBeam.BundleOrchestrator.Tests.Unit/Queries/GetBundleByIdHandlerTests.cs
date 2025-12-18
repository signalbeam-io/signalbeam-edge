using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Queries;

public class GetBundleByIdHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly GetBundleByIdHandler _handler;

    public GetBundleByIdHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _handler = new GetBundleByIdHandler(_bundleRepository, _bundleVersionRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnBundleWithVersions_WhenBundleExists()
    {
        // Arrange
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            "Test bundle description",
            DateTimeOffset.UtcNow);

        bundle.UpdateLatestVersion(BundleVersion.Parse("2.0.0"));

        var versions = new List<AppBundleVersion>
        {
            AppBundleVersion.Create(
                Guid.NewGuid(),
                new BundleId(bundleId),
                BundleVersion.Parse("1.0.0"),
                new List<ContainerSpec> { ContainerSpec.Create("web", "nginx:1.21") },
                "Initial release",
                DateTimeOffset.UtcNow.AddDays(-7)),
            AppBundleVersion.Create(
                Guid.NewGuid(),
                new BundleId(bundleId),
                BundleVersion.Parse("2.0.0"),
                new List<ContainerSpec> { ContainerSpec.Create("web", "nginx:1.22"), ContainerSpec.Create("api", "app:2.0") },
                "Major update",
                DateTimeOffset.UtcNow)
        };

        var query = new GetBundleByIdQuery(bundleId.ToString());

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetAllVersionsAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(versions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Bundle.BundleId.Should().Be(bundleId);
        result.Value.Bundle.Name.Should().Be("test-bundle");
        result.Value.Bundle.LatestVersion.Should().Be("2.0.0");
        result.Value.Bundle.Versions.Should().HaveCount(2);
        result.Value.Bundle.Versions[0].Version.Should().Be("1.0.0");
        result.Value.Bundle.Versions[0].ContainerCount.Should().Be(1);
        result.Value.Bundle.Versions[1].Version.Should().Be("2.0.0");
        result.Value.Bundle.Versions[1].ContainerCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBundleNotFound()
    {
        // Arrange
        var query = new GetBundleByIdQuery(Guid.NewGuid().ToString());

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("BUNDLE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInvalidBundleIdFormat()
    {
        // Arrange
        var query = new GetBundleByIdQuery("invalid-guid");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_BUNDLE_ID");
    }
}
