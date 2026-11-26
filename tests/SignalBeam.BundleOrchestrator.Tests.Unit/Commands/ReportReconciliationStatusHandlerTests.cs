using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Validators;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.InternalServices;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class ReportReconciliationStatusHandlerTests
{
    private readonly IRolloutStatusRepository _repository = Substitute.For<IRolloutStatusRepository>();
    private readonly IDeviceTenantClient _tenantClient = Substitute.For<IDeviceTenantClient>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ReportReconciliationStatusHandler _handler;
    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly BundleId _bundleId = new(Guid.NewGuid());

    public ReportReconciliationStatusHandlerTests()
    {
        _handler = new ReportReconciliationStatusHandler(_repository, _tenantClient);
        _tenantClient.LookupAsync(_deviceId, Arg.Any<CancellationToken>()).Returns(DeviceTenantLookup.Found(_tenantId));
    }

    [Fact]
    public async Task Handle_Failed_MarksRolloutStatusFailedWithErrors()
    {
        // Arrange
        var rolloutStatus = TrackedRolloutStatus();
        var command = new ReportReconciliationStatusCommand(
            _deviceId, "failed", _bundleId.ToString(), "1.2.0", ["image pull failed: nginx:9.9", "container exited"]);

        // Act
        var result = await _handler.Handle(command, _tenantId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Tracked.Should().BeTrue();
        result.Value.RolloutStatus.Should().Be("Failed");
        rolloutStatus.Status.Should().Be(RolloutState.Failed);
        rolloutStatus.ErrorMessage.Should().Be("image pull failed: nginx:9.9; container exited");
        await _repository.Received(1).UpdateAsync(rolloutStatus, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedWithoutErrors_UsesGenericMessage()
    {
        // Arrange
        var rolloutStatus = TrackedRolloutStatus();

        // Act
        await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        rolloutStatus.ErrorMessage.Should().Be("Reconciliation failed on device.");
    }

    [Fact]
    public async Task Handle_Succeeded_MarksRolloutStatusSucceeded()
    {
        // Arrange
        var rolloutStatus = TrackedRolloutStatus();

        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "Succeeded", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        result.Value!.RolloutStatus.Should().Be("Succeeded");
        rolloutStatus.Status.Should().Be(RolloutState.Succeeded);
        rolloutStatus.CompletedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("not-a-guid", "1.2.0")]
    [InlineData("3f0b4c2e-8f8e-4c5e-9a55-7b6f4f0a1c11", "not-semver")]
    public async Task Handle_NoUsableBundleReference_IsUntracked(string? bundleId, string? version)
    {
        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", bundleId, version),
            _tenantId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Tracked.Should().BeFalse();
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_NoRolloutRecord_IsUntracked()
    {
        // Arrange
        _repository.GetByDeviceAndBundleAsync(
                Arg.Any<DeviceId>(), Arg.Any<BundleId>(), Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns((RolloutStatus?)null);

        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        result.Value!.Tracked.Should().BeFalse();
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UnknownStatus_ReturnsValidationError()
    {
        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "exploded", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("INVALID_RECONCILIATION_STATUS");
    }

    [Fact]
    public async Task Handle_CancelledRollout_IsLeftCancelled()
    {
        // Arrange
        var rolloutStatus = TrackedRolloutStatus();
        rolloutStatus.MarkCancelled(DateTimeOffset.UtcNow);

        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "succeeded", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        result.Value!.Tracked.Should().BeFalse();
        result.Value.RolloutStatus.Should().Be("Cancelled");
        rolloutStatus.Status.Should().Be(RolloutState.Cancelled);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_DeviceOfAnotherTenant_ReturnsNotFoundWithoutTouchingRollout()
    {
        // Arrange
        var rolloutStatus = TrackedRolloutStatus();

        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", _bundleId.ToString(), "1.2.0"),
            Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Error!.Code.Should().Be("DEVICE_NOT_FOUND");
        rolloutStatus.Status.Should().Be(RolloutState.Pending);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(DeviceTenantLookupStatus.NotFound, "DEVICE_NOT_FOUND")]
    [InlineData(DeviceTenantLookupStatus.Unavailable, "DEVICE_LOOKUP_UNAVAILABLE")]
    public async Task Handle_DeviceNotVerified_ReturnsError(DeviceTenantLookupStatus status, string expectedCode)
    {
        // Arrange
        _tenantClient.LookupAsync(_deviceId, Arg.Any<CancellationToken>())
            .Returns(new DeviceTenantLookup(status, Guid.Empty));

        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", _bundleId.ToString(), "1.2.0"),
            _tenantId, CancellationToken.None);

        // Assert
        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task Handle_CallerWithoutTenant_ReturnsForbidden()
    {
        // Act
        var result = await _handler.Handle(
            new ReportReconciliationStatusCommand(_deviceId, "failed", _bundleId.ToString(), "1.2.0"),
            null, CancellationToken.None);

        // Assert
        result.Error!.Code.Should().Be("TENANT_REQUIRED");
        await _tenantClient.DidNotReceiveWithAnyArgs().LookupAsync(default, default);
    }

    [Theory]
    [InlineData("succeeded", true)]
    [InlineData("FAILED", true)]
    [InlineData("pending", false)]
    [InlineData("", false)]
    public void Validator_Status_AcceptsOnlySucceededOrFailed(string status, bool valid)
    {
        // Act
        var result = new ReportReconciliationStatusValidator().Validate(
            new ReportReconciliationStatusCommand(_deviceId, status, null, null));

        // Assert
        result.IsValid.Should().Be(valid);
    }

    private RolloutStatus TrackedRolloutStatus()
    {
        var rolloutStatus = RolloutStatus.Create(
            Guid.NewGuid(), Guid.NewGuid(), _bundleId, BundleVersion.Parse("1.2.0"), new DeviceId(_deviceId), DateTimeOffset.UtcNow);

        _repository.GetByDeviceAndBundleAsync(
                new DeviceId(_deviceId), _bundleId, Arg.Is<BundleVersion>(v => v.ToString() == "1.2.0"), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        return rolloutStatus;
    }
}
