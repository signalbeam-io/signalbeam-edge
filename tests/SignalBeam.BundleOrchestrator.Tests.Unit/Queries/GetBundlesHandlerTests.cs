using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Queries;

public class GetBundlesHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly GetBundlesHandler _handler;

    public GetBundlesHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _handler = new GetBundlesHandler(_bundleRepository, _bundleVersionRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllBundles_WhenBundlesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var bundles = new List<AppBundle>
        {
            AppBundle.Create(
                new BundleId(Guid.NewGuid()),
                new TenantId(tenantId),
                "bundle-1",
                "First bundle",
                DateTimeOffset.UtcNow),
            AppBundle.Create(
                new BundleId(Guid.NewGuid()),
                new TenantId(tenantId),
                "bundle-2",
                "Second bundle",
                DateTimeOffset.UtcNow)
        };

        bundles[0].UpdateLatestVersion(BundleVersion.Parse("1.0.0"));

        var query = new GetBundlesQuery(tenantId);

        _bundleRepository.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(bundles);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Bundles.Should().HaveCount(2);
        result.Value.Bundles[0].Name.Should().Be("bundle-1");
        result.Value.Bundles[0].LatestVersion.Should().Be("1.0.0");
        result.Value.Bundles[1].Name.Should().Be("bundle-2");
        result.Value.Bundles[1].LatestVersion.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoBundlesExist()
    {
        // Arrange
        var query = new GetBundlesQuery(Guid.NewGuid());

        _bundleRepository.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new List<AppBundle>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Bundles.Should().BeEmpty();
    }
}
