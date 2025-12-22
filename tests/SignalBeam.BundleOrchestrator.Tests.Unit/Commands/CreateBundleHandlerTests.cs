using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class CreateBundleHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly CreateBundleHandler _handler;

    public CreateBundleHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _handler = new CreateBundleHandler(_bundleRepository, _bundleVersionRepository);
    }

    [Fact]
    public async Task Handle_ShouldCreateBundle_WhenBundleDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new CreateBundleCommand(
            TenantId: tenantId,
            Name: "warehouse-monitor",
            Description: "Warehouse monitoring application");

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(command.Name);
        result.Value.Description.Should().Be(command.Description);
        result.Value.BundleId.Should().NotBeEmpty();

        await _bundleRepository.Received(1).AddAsync(
            Arg.Is<AppBundle>(b => b.Name == command.Name && b.TenantId.Value == tenantId),
            Arg.Any<CancellationToken>());

        await _bundleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBundleAlreadyExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new CreateBundleCommand(
            TenantId: tenantId,
            Name: "warehouse-monitor");

        var existingBundle = AppBundle.Create(
            new BundleId(Guid.NewGuid()),
            new TenantId(tenantId),
            "warehouse-monitor",
            "Existing bundle",
            DateTimeOffset.UtcNow);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(existingBundle);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("BUNDLE_ALREADY_EXISTS");
        result.Error.Type.Should().Be(SignalBeam.Shared.Infrastructure.Results.ErrorType.Conflict);

        await _bundleRepository.DidNotReceive().AddAsync(
            Arg.Any<AppBundle>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateBundleWithoutDescription_WhenDescriptionIsNull()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "minimal-bundle",
            Description: null);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCreateBundleWithVersion_WhenVersionAndContainersProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var containers = new List<ContainerSpecDto>
        {
            new ContainerSpecDto(
                Name: "nginx",
                Image: "nginx:latest",
                Environment: new Dictionary<string, string> { { "ENV", "prod" } },
                Ports: new List<string> { "8080:80" })
        };

        var command = new CreateBundleCommand(
            TenantId: tenantId,
            Name: "web-app",
            Description: "Web application",
            Version: "1.0.0",
            Containers: containers);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(command.Name);
        result.Value.Version.Should().Be("1.0.0");
        result.Value.VersionId.Should().NotBeNull();

        await _bundleRepository.Received(1).AddAsync(
            Arg.Any<AppBundle>(),
            Arg.Any<CancellationToken>());

        await _bundleVersionRepository.Received(1).AddAsync(
            Arg.Is<AppBundleVersion>(v => v.Version.ToString() == "1.0.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenVersionProvidedWithoutContainers()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "invalid-bundle",
            Version: "1.0.0",
            Containers: null);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_VERSION_DATA");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenContainersProvidedWithoutVersion()
    {
        // Arrange
        var containers = new List<ContainerSpecDto>
        {
            new ContainerSpecDto(Name: "nginx", Image: "nginx:latest")
        };

        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "invalid-bundle",
            Version: null,
            Containers: containers);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_VERSION_DATA");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenVersionFormatIsInvalid()
    {
        // Arrange
        var containers = new List<ContainerSpecDto>
        {
            new ContainerSpecDto(Name: "nginx", Image: "nginx:latest")
        };

        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "invalid-version-bundle",
            Version: "invalid-version",
            Containers: containers);

        _bundleRepository.GetByNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppBundle?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_VERSION");
    }
}
