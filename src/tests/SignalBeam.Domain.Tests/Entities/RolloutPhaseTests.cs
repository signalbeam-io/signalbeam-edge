using FluentAssertions;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class RolloutPhaseTests
{
    private readonly Guid _rolloutId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldCreatePendingPhase_WithValidInputs()
    {
        // Arrange
        var phaseNumber = 1;
        var name = "Canary";
        var targetDeviceCount = 10;
        var targetPercentage = 10m;
        var minHealthyDuration = TimeSpan.FromMinutes(5);

        // Act
        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            _rolloutId,
            phaseNumber,
            name,
            targetDeviceCount,
            targetPercentage,
            minHealthyDuration);

        // Assert
        phase.RolloutId.Should().Be(_rolloutId);
        phase.PhaseNumber.Should().Be(phaseNumber);
        phase.Name.Should().Be(name);
        phase.TargetDeviceCount.Should().Be(targetDeviceCount);
        phase.TargetPercentage.Should().Be(targetPercentage);
        phase.MinHealthyDuration.Should().Be(minHealthyDuration);
        phase.Status.Should().Be(PhaseStatus.Pending);
        phase.SuccessCount.Should().Be(0);
        phase.FailureCount.Should().Be(0);
    }

    [Fact]
    public void Start_ShouldTransitionToInProgress()
    {
        // Arrange
        var phase = CreateTestPhase();
        var startedAt = DateTimeOffset.UtcNow;

        // Act
        phase.Start(startedAt);

        // Assert
        phase.Status.Should().Be(PhaseStatus.InProgress);
        phase.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void Start_ShouldThrowInvalidOperationException_WhenNotPending()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.Start(DateTimeOffset.UtcNow);

        // Act
        var act = () => phase.Start(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot start phase in InProgress status.");
    }

    [Fact]
    public void Complete_ShouldTransitionToCompleted()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.Start(DateTimeOffset.UtcNow);
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(10);

        // Act
        phase.Complete(completedAt);

        // Assert
        phase.Status.Should().Be(PhaseStatus.Completed);
        phase.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Complete_ShouldThrowInvalidOperationException_WhenNotInProgress()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        var act = () => phase.Complete(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot complete phase in Pending status.");
    }

    [Fact]
    public void Fail_ShouldTransitionToFailed()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.Start(DateTimeOffset.UtcNow);

        // Act
        phase.Fail();

        // Assert
        phase.Status.Should().Be(PhaseStatus.Failed);
    }

    [Fact]
    public void Skip_ShouldTransitionToSkipped()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        phase.Skip();

        // Assert
        phase.Status.Should().Be(PhaseStatus.Skipped);
    }

    [Fact]
    public void IncrementSuccessCount_ShouldIncreaseCount()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();

        // Assert
        phase.SuccessCount.Should().Be(2);
    }

    [Fact]
    public void IncrementFailureCount_ShouldIncreaseCount()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        phase.IncrementFailureCount();
        phase.IncrementFailureCount();
        phase.IncrementFailureCount();

        // Assert
        phase.FailureCount.Should().Be(3);
    }

    [Fact]
    public void GetSuccessRate_ShouldReturnZero_WhenNoDevices()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        var successRate = phase.GetSuccessRate();

        // Assert
        successRate.Should().Be(0m);
    }

    [Fact]
    public void GetSuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();
        phase.IncrementFailureCount();

        // Act
        var successRate = phase.GetSuccessRate();

        // Assert
        successRate.Should().BeApproximately(0.6667m, 0.001m); // 2/3
    }

    [Fact]
    public void GetFailureRate_ShouldReturnZero_WhenNoDevices()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        var failureRate = phase.GetFailureRate();

        // Assert
        failureRate.Should().Be(0m);
    }

    [Fact]
    public void GetFailureRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.IncrementSuccessCount();
        phase.IncrementFailureCount();
        phase.IncrementFailureCount();

        // Act
        var failureRate = phase.GetFailureRate();

        // Assert
        failureRate.Should().BeApproximately(0.6667m, 0.001m); // 2/3
    }

    [Fact]
    public void HasMetTargetDeviceCount_ShouldReturnTrue_WhenTargetMet()
    {
        // Arrange
        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            _rolloutId,
            1,
            "Test",
            targetDeviceCount: 3,
            targetPercentage: 100m,
            minHealthyDuration: null);

        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();
        phase.IncrementFailureCount();

        // Act
        var hasMetTarget = phase.HasMetTargetDeviceCount();

        // Assert
        hasMetTarget.Should().BeTrue();
    }

    [Fact]
    public void HasMetTargetDeviceCount_ShouldReturnFalse_WhenTargetNotMet()
    {
        // Arrange
        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            _rolloutId,
            1,
            "Test",
            targetDeviceCount: 5,
            targetPercentage: 100m,
            minHealthyDuration: null);

        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();

        // Act
        var hasMetTarget = phase.HasMetTargetDeviceCount();

        // Assert
        hasMetTarget.Should().BeFalse();
    }

    [Fact]
    public void IsHealthy_ShouldReturnTrue_WhenBelowThreshold()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();
        phase.IncrementSuccessCount();
        phase.IncrementFailureCount(); // 1/5 = 20% failure rate

        // Act
        var isHealthy = phase.IsHealthy(failureThreshold: 0.25m); // 25% threshold

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_ShouldReturnFalse_WhenAboveThreshold()
    {
        // Arrange
        var phase = CreateTestPhase();
        phase.IncrementSuccessCount();
        phase.IncrementFailureCount();
        phase.IncrementFailureCount();
        phase.IncrementFailureCount(); // 3/4 = 75% failure rate

        // Act
        var isHealthy = phase.IsHealthy(failureThreshold: 0.10m); // 10% threshold

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public void IsHealthy_ShouldReturnTrue_WhenNoDevices()
    {
        // Arrange
        var phase = CreateTestPhase();

        // Act
        var isHealthy = phase.IsHealthy(failureThreshold: 0.05m);

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void AddDeviceAssignment_ShouldAddToCollection()
    {
        // Arrange
        var phase = CreateTestPhase();
        var deviceId = new DeviceId(Guid.NewGuid());

        var assignment = RolloutDeviceAssignment.Create(
            Guid.NewGuid(),
            _rolloutId,
            phase.Id,
            deviceId);

        // Act
        phase.AddDeviceAssignment(assignment);

        // Assert
        phase.DeviceAssignments.Should().HaveCount(1);
        phase.DeviceAssignments.First().Should().Be(assignment);
    }

    // Helper methods

    private RolloutPhase CreateTestPhase()
    {
        return RolloutPhase.Create(
            Guid.NewGuid(),
            _rolloutId,
            1,
            "Test Phase",
            targetDeviceCount: 10,
            targetPercentage: 10m,
            minHealthyDuration: TimeSpan.FromMinutes(5));
    }
}
