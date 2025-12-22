using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

public class UpdateRolloutStatusHandlerTests
{
    private readonly IRolloutStatusRepository _rolloutStatusRepository;
    private readonly UpdateRolloutStatusHandler _handler;

    public UpdateRolloutStatusHandlerTests()
    {
        _rolloutStatusRepository = Substitute.For<IRolloutStatusRepository>();
        _handler = new UpdateRolloutStatusHandler(_rolloutStatusRepository);
    }

    [Fact]
    public async Task Handle_ShouldMarkRolloutAsInProgress_WhenStatusIsInProgress()
    {
        // Arrange
        var rolloutStatusId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var rolloutStatus = RolloutStatus.Create(
            rolloutStatusId,
            rolloutId,
            new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"),
            new DeviceId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);

        var command = new UpdateRolloutStatusCommand(
            RolloutId: rolloutStatusId,
            Status: "InProgress");

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be("InProgress");
        rolloutStatus.Status.Should().Be(RolloutState.InProgress);

        await _rolloutStatusRepository.Received(1).UpdateAsync(
            Arg.Is<RolloutStatus>(rs => rs.Id == rolloutStatusId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMarkRolloutAsSucceeded_WhenStatusIsSucceeded()
    {
        // Arrange
        var rolloutStatusId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var rolloutStatus = RolloutStatus.Create(
            rolloutStatusId,
            rolloutId,
            new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"),
            new DeviceId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);
        rolloutStatus.MarkInProgress();

        var command = new UpdateRolloutStatusCommand(
            RolloutId: rolloutStatusId,
            Status: "Succeeded");

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Succeeded");
        result.Value.CompletedAt.Should().NotBeNull();
        rolloutStatus.Status.Should().Be(RolloutState.Succeeded);
        rolloutStatus.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldMarkRolloutAsFailed_WhenStatusIsFailedWithErrorMessage()
    {
        // Arrange
        var rolloutStatusId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var rolloutStatus = RolloutStatus.Create(
            rolloutStatusId,
            rolloutId,
            new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"),
            new DeviceId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);
        rolloutStatus.MarkInProgress();

        var command = new UpdateRolloutStatusCommand(
            RolloutId: rolloutStatusId,
            Status: "Failed",
            ErrorMessage: "Container failed to start");

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Failed");
        rolloutStatus.Status.Should().Be(RolloutState.Failed);
        rolloutStatus.ErrorMessage.Should().Be("Container failed to start");
        rolloutStatus.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRolloutNotFound()
    {
        // Arrange
        var command = new UpdateRolloutStatusCommand(
            RolloutId: Guid.NewGuid(),
            Status: "Succeeded");

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RolloutStatus?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ROLLOUT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenStatusIsInvalid()
    {
        // Arrange
        var rolloutStatusId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var rolloutStatus = RolloutStatus.Create(
            rolloutStatusId,
            rolloutId,
            new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"),
            new DeviceId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);

        var command = new UpdateRolloutStatusCommand(
            RolloutId: rolloutStatusId,
            Status: "InvalidStatus");

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_STATUS");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenFailedStatusHasNoErrorMessage()
    {
        // Arrange
        var rolloutStatusId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var rolloutStatus = RolloutStatus.Create(
            rolloutStatusId,
            rolloutId,
            new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"),
            new DeviceId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);

        var command = new UpdateRolloutStatusCommand(
            RolloutId: rolloutStatusId,
            Status: "Failed",
            ErrorMessage: null);

        _rolloutStatusRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rolloutStatus);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ERROR_MESSAGE_REQUIRED");
    }
}
