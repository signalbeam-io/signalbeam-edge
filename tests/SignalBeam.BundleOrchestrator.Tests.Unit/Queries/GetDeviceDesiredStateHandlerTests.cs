using SignalBeam.BundleOrchestrator.Application.Queries;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Queries;

public class GetDeviceDesiredStateHandlerTests
{
    private readonly IDeviceDesiredStateRepository _desiredStates = Substitute.For<IDeviceDesiredStateRepository>();
    private readonly IBundleVersionRepository _versions = Substitute.For<IBundleVersionRepository>();
    private readonly IDeviceTenantClient _tenantClient = Substitute.For<IDeviceTenantClient>();
    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly BundleId _bundleId = new(Guid.NewGuid());
    private readonly GetDeviceDesiredStateHandler _handler;

    public GetDeviceDesiredStateHandlerTests()
    {
        _handler = new GetDeviceDesiredStateHandler(_desiredStates, _versions, new DeviceOwnershipVerifier(_tenantClient));
        _tenantClient.LookupAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(DeviceTenantLookup.Found(_tenantId));
    }

    private DeviceCaller Device => new(_tenantId, _deviceId);

    [Fact]
    public async Task Handle_Assigned_ReturnsBundleWithContainers()
    {
        // Arrange
        var version = BundleVersion.Parse("1.2.0");
        _desiredStates.GetByDeviceIdAsync(new DeviceId(_deviceId), Arg.Any<CancellationToken>())
            .Returns(DeviceDesiredState.Create(Guid.NewGuid(), new DeviceId(_deviceId), _bundleId, version, "ops@example.com", DateTimeOffset.UtcNow));
        _versions.GetByBundleAndVersionAsync(_bundleId, Arg.Is<BundleVersion>(v => v.ToString() == "1.2.0"), Arg.Any<CancellationToken>())
            .Returns(AppBundleVersion.Create(
                Guid.NewGuid(), _bundleId, version,
                [ContainerSpec.Create("web", "nginx:1.27", "{\"A\":\"1\"}", "[\"8080:80\"]")],
                null, DateTimeOffset.UtcNow));

        // Act
        var result = await _handler.Handle(new GetDeviceDesiredStateQuery(_deviceId), Device, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BundleId.Should().Be(_bundleId.Value.ToString());
        result.Value.BundleVersion.Should().Be("1.2.0");
        result.Value.Containers.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DesiredContainerDto(
                "web", "nginx:1.27",
                new Dictionary<string, string> { ["A"] = "1" },
                new Dictionary<string, string> { ["80/tcp"] = "8080" },
                null));
    }

    [Fact]
    public async Task Handle_NothingAssigned_ReturnsEmptyState()
    {
        // Act
        var result = await _handler.Handle(new GetDeviceDesiredStateQuery(_deviceId), Device, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BundleId.Should().BeNull();
        result.Value.Containers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AssignedVersionMissing_FailsInsteadOfReturningEmptyState()
    {
        // Arrange
        _desiredStates.GetByDeviceIdAsync(new DeviceId(_deviceId), Arg.Any<CancellationToken>())
            .Returns(DeviceDesiredState.Create(Guid.NewGuid(), new DeviceId(_deviceId), _bundleId, BundleVersion.Parse("9.9.9"), null, DateTimeOffset.UtcNow));

        // Act
        var result = await _handler.Handle(new GetDeviceDesiredStateQuery(_deviceId), Device, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public async Task Handle_OperatorOfAnotherTenant_ReturnsNotFound()
    {
        // Act
        var result = await _handler.Handle(
            new GetDeviceDesiredStateQuery(_deviceId), new DeviceCaller(Guid.NewGuid(), null), CancellationToken.None);

        // Assert
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
        await _desiredStates.DidNotReceive().GetByDeviceIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>());
    }
}
