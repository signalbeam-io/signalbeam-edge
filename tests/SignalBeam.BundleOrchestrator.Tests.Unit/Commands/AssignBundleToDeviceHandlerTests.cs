using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class AssignBundleToDeviceHandlerTests
{
    private readonly IBundleRepository _bundleRepository;
    private readonly IBundleVersionRepository _bundleVersionRepository;
    private readonly IDeviceDesiredStateRepository _desiredStateRepository;
    private readonly IRolloutStatusRepository _rolloutStatusRepository;
    private readonly AssignBundleToDeviceHandler _handler;

    public AssignBundleToDeviceHandlerTests()
    {
        _bundleRepository = Substitute.For<IBundleRepository>();
        _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
        _desiredStateRepository = Substitute.For<IDeviceDesiredStateRepository>();
        _rolloutStatusRepository = Substitute.For<IRolloutStatusRepository>();
        _handler = new AssignBundleToDeviceHandler(
            _bundleRepository,
            _bundleVersionRepository,
            _desiredStateRepository,
            _rolloutStatusRepository);
    }

    [Fact]
    public async Task Handle_ShouldAssignBundle_WhenDeviceHasNoDesiredState()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        var bundleVersion = AppBundleVersion.Create(
            Guid.NewGuid(),
            new BundleId(bundleId),
            BundleVersion.Parse("1.0.0"),
            new List<ContainerSpec> { ContainerSpec.Create("web", "nginx:1.21") },
            null,
            DateTimeOffset.UtcNow);

        var command = new AssignBundleToDeviceCommand(
            DeviceId: deviceId.ToString(),
            BundleId: bundleId.ToString(),
            Version: "1.0.0",
            AssignedBy: "admin");

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetByBundleAndVersionAsync(Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns(bundleVersion);

        _desiredStateRepository.GetByDeviceIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((DeviceDesiredState?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DeviceId.Should().Be(deviceId);
        result.Value.BundleId.Should().Be(bundleId);
        result.Value.Version.Should().Be("1.0.0");

        await _desiredStateRepository.Received(1).AddAsync(
            Arg.Is<DeviceDesiredState>(ds => ds.DeviceId.Value == deviceId && ds.BundleId.Value == bundleId),
            Arg.Any<CancellationToken>());

        await _rolloutStatusRepository.Received(1).AddAsync(
            Arg.Is<RolloutStatus>(rs => rs.DeviceId.Value == deviceId && rs.BundleId.Value == bundleId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateDesiredState_WhenDeviceAlreadyHasDesiredState()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        var bundleVersion = AppBundleVersion.Create(
            Guid.NewGuid(),
            new BundleId(bundleId),
            BundleVersion.Parse("2.0.0"),
            new List<ContainerSpec> { ContainerSpec.Create("web", "nginx:1.22") },
            null,
            DateTimeOffset.UtcNow);

        var existingDesiredState = DeviceDesiredState.Create(
            Guid.NewGuid(),
            new DeviceId(deviceId),
            new BundleId(bundleId),
            BundleVersion.Parse("1.0.0"),
            "system",
            DateTimeOffset.UtcNow.AddDays(-1));

        var command = new AssignBundleToDeviceCommand(
            DeviceId: deviceId.ToString(),
            BundleId: bundleId.ToString(),
            Version: "2.0.0",
            AssignedBy: "admin");

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetByBundleAndVersionAsync(Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns(bundleVersion);

        _desiredStateRepository.GetByDeviceIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(existingDesiredState);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _desiredStateRepository.Received(1).UpdateAsync(
            Arg.Is<DeviceDesiredState>(ds => ds.DeviceId.Value == deviceId),
            Arg.Any<CancellationToken>());

        await _desiredStateRepository.DidNotReceive().AddAsync(
            Arg.Any<DeviceDesiredState>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBundleNotFound()
    {
        // Arrange
        var command = new AssignBundleToDeviceCommand(
            DeviceId: Guid.NewGuid().ToString(),
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0");

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
    public async Task Handle_ShouldReturnFailure_WhenVersionNotFound()
    {
        // Arrange
        var bundleId = Guid.NewGuid();
        var bundle = AppBundle.Create(
            new BundleId(bundleId),
            new TenantId(Guid.NewGuid()),
            "test-bundle",
            null,
            DateTimeOffset.UtcNow);

        var command = new AssignBundleToDeviceCommand(
            DeviceId: Guid.NewGuid().ToString(),
            BundleId: bundleId.ToString(),
            Version: "99.99.99");

        _bundleRepository.GetByIdAsync(Arg.Any<BundleId>(), Arg.Any<CancellationToken>())
            .Returns(bundle);

        _bundleVersionRepository.GetByBundleAndVersionAsync(Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns((AppBundleVersion?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VERSION_NOT_FOUND");
    }
}
