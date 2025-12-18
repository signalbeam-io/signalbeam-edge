using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class CreateBundleVersionHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly CreateBundleVersionHandler _handler;

    public CreateBundleVersionHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _handler = new CreateBundleVersionHandler(_bundleRepository, _bundleVersionRepository);
    }

    [Fact]
    public async Task Handle_ShouldCreateBundleVersion_WhenValid()
    {
        // Arrange
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            "Test bundle",
            DateTimeOffset.UtcNow);

        var containers = new List<ContainerSpecDto>
        {
            new ContainerSpecDto("web", "nginx:1.21", new Dictionary<string, string> { ["PORT"] = "80" }, new List<string> { "80:80" }),
            new ContainerSpecDto("api", "myapp:1.0.0")
        };

        var command = new CreateBundleVersionCommand(
            BundleId: bundleId.ToString(),
            Version: "1.0.0",
            Containers: containers,
            ReleaseNotes: "Initial release");

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetByBundleAndVersionAsync(Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns((AppBundleVersion?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.BundleId.Should().Be(bundleId);
        result.Value.Version.Should().Be("1.0.0");
        result.Value.ContainerCount.Should().Be(2);

        await _bundleVersionRepository.Received(1).AddAsync(
            Arg.Is<AppBundleVersion>(v => v.BundleId.Value == bundleId && v.Version.ToString() == "1.0.0"),
            Arg.Any<CancellationToken>());

        await _bundleRepository.Received(1).UpdateAsync(
            Arg.Is<AppBundle>(b => b.Id.Value == bundleId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBundleNotFound()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("BUNDLE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenVersionAlreadyExists()
    {
        // Arrange
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        var existingVersion = AppBundleVersion.Create(
            Guid.NewGuid(),
            new BundleId(bundleId),
            BundleVersion.Parse("1.0.0"),
            new List<ContainerSpec> { ContainerSpec.Create("web", "nginx:1.21") },
            null,
            DateTimeOffset.UtcNow);

        var command = new CreateBundleVersionCommand(
            BundleId: bundleId.ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetByBundleAndVersionAsync(Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns(existingVersion);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VERSION_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNoContainersProvided()
    {
        // Arrange
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        var command = new CreateBundleVersionCommand(
            BundleId: bundleId.ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto>());

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NO_CONTAINERS");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInvalidVersionFormat()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "invalid-version",
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        var bundle = AppBundle.Create(
            BundleId.Parse(command.BundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_VERSION");
    }
}
