using FluentAssertions;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.Events;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class RolloutTests
{
    private readonly TenantId _tenantId = new(Guid.NewGuid());
    private readonly BundleId _bundleId = BundleId.New();
    private readonly BundleVersion _targetVersion = BundleVersion.Parse("1.0.0");
    private readonly BundleVersion _previousVersion = BundleVersion.Parse("0.9.0");
    private readonly DeviceGroupId _deviceGroupId = new(Guid.NewGuid());

    [Fact]
    public void Create_ShouldCreatePendingRollout_WithValidInputs()
    {
        // Arrange
        var name = "Production Rollout";
        var description = "Rolling out v1.0.0 to production";
        var failureThreshold = 0.05m;
        var createdBy = "admin@test.com";

        // Act
        var rollout = Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            _bundleId,
            _targetVersion,
            _previousVersion,
            name,
            description,
            _deviceGroupId,
            createdBy,
            failureThreshold,
            DateTimeOffset.UtcNow);

        // Assert
        rollout.TenantId.Should().Be(_tenantId);
        rollout.BundleId.Should().Be(_bundleId);
        rollout.TargetVersion.Should().Be(_targetVersion);
        rollout.PreviousVersion.Should().Be(_previousVersion);
        rollout.Name.Should().Be(name);
        rollout.Description.Should().Be(description);
        rollout.TargetDeviceGroupId.Should().Be(_deviceGroupId);
        rollout.Status.Should().Be(RolloutLifecycleStatus.Pending);
        rollout.CurrentPhaseNumber.Should().Be(0);
        rollout.FailureThreshold.Should().Be(failureThreshold);
        rollout.CreatedBy.Should().Be(createdBy);
        rollout.Phases.Should().BeEmpty(); // Phases are added separately
    }

    [Fact]
    public void Start_ShouldTransitionToInProgress()
    {
        // Arrange
        var rollout = CreateTestRollout();
        var startedAt = DateTimeOffset.UtcNow;

        // Act
        rollout.Start(startedAt);

        // Assert
        rollout.Status.Should().Be(RolloutLifecycleStatus.InProgress);
        rollout.StartedAt.Should().Be(startedAt);
        // Note: CurrentPhaseNumber is still 0, phases need to be started separately
    }

    [Fact]
    public void Start_ShouldThrowInvalidOperationException_WhenAlreadyStarted()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Act
        var act = () => rollout.Start(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot start rollout in InProgress status.");
    }

    [Fact]
    public void Pause_ShouldTransitionToPaused_WhenInProgress()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Act
        rollout.Pause();

        // Assert
        rollout.Status.Should().Be(RolloutLifecycleStatus.Paused);
    }

    [Fact]
    public void Pause_ShouldThrowInvalidOperationException_WhenNotInProgress()
    {
        // Arrange
        var rollout = CreateTestRollout();

        // Act
        var act = () => rollout.Pause();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot pause rollout in Pending status.");
    }

    [Fact]
    public void Resume_ShouldTransitionBackToInProgress_WhenPaused()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);
        rollout.Pause();
        var resumedAt = DateTimeOffset.UtcNow.AddMinutes(10);

        // Act
        rollout.Resume(resumedAt);

        // Assert
        rollout.Status.Should().Be(RolloutLifecycleStatus.InProgress);
    }

    [Fact]
    public void Resume_ShouldThrowInvalidOperationException_WhenNotPaused()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Act
        var act = () => rollout.Resume(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot resume rollout in InProgress status.");
    }

    [Fact]
    public void Rollback_ShouldTransitionToRolledBack()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);
        var rolledBackAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        rollout.Rollback(rolledBackAt);

        // Assert
        rollout.Status.Should().Be(RolloutLifecycleStatus.RolledBack);
        rollout.CompletedAt.Should().Be(rolledBackAt);
    }

    [Fact]
    public void AdvancePhase_ShouldMoveToNextPhase()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Start and complete phase 1
        rollout.StartCurrentPhase(DateTimeOffset.UtcNow);
        rollout.CompleteCurrentPhase(DateTimeOffset.UtcNow);

        // Act
        rollout.AdvancePhase();

        // Assert
        rollout.CurrentPhaseNumber.Should().Be(1); // 0-indexed, so phase 2 is at index 1
        var currentPhase = rollout.GetCurrentPhase();
        currentPhase.Should().NotBeNull();
        currentPhase!.PhaseNumber.Should().Be(2);
        currentPhase.Status.Should().Be(PhaseStatus.Pending); // Not started yet, AdvancePhase doesn't auto-start
    }

    [Fact]
    public void CompleteCurrentPhase_ShouldCompleteRollout_WhenLastPhaseCompleted()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Start and complete phase 1
        rollout.StartCurrentPhase(DateTimeOffset.UtcNow);
        rollout.CompleteCurrentPhase(DateTimeOffset.UtcNow);
        rollout.AdvancePhase(); // Move to phase 2

        // Start and complete phase 2 (last phase)
        rollout.StartCurrentPhase(DateTimeOffset.UtcNow);
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(30);

        // Act
        rollout.CompleteCurrentPhase(completedAt); // Should automatically complete rollout

        // Assert
        rollout.Status.Should().Be(RolloutLifecycleStatus.Completed);
        rollout.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void CompleteCurrentPhase_ShouldMarkPhaseAsCompleted()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Start the first phase
        var phaseStartedAt = DateTimeOffset.UtcNow;
        rollout.StartCurrentPhase(phaseStartedAt);

        var completedAt = DateTimeOffset.UtcNow.AddMinutes(30);

        // Act
        rollout.CompleteCurrentPhase(completedAt);

        // Assert
        var phase1 = rollout.Phases.First(p => p.PhaseNumber == 1);
        phase1.Status.Should().Be(PhaseStatus.Completed);
        phase1.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void GetOverallProgress_ShouldCalculateCorrectly()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);
        rollout.StartCurrentPhase(DateTimeOffset.UtcNow);

        var phase1 = rollout.GetCurrentPhase()!;
        phase1.IncrementSuccessCount();
        phase1.IncrementSuccessCount();
        phase1.IncrementFailureCount();

        // Act
        var (total, succeeded, failed, successRate) = rollout.GetOverallProgress();

        // Assert
        total.Should().Be(3);
        succeeded.Should().Be(2);
        failed.Should().Be(1);
        successRate.Should().BeApproximately(0.6667m, 0.001m);
    }

    [Fact]
    public void GetCurrentPhase_ShouldReturnNull_WhenNotStarted()
    {
        // Arrange - Create rollout without phases
        var rollout = Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "Test Rollout",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        // Act
        var currentPhase = rollout.GetCurrentPhase();

        // Assert
        currentPhase.Should().BeNull();
    }

    [Fact]
    public void GetCurrentPhase_ShouldReturnCorrectPhase_WhenInProgress()
    {
        // Arrange
        var rollout = CreateTestRollout();
        rollout.Start(DateTimeOffset.UtcNow);

        // Act
        var currentPhase = rollout.GetCurrentPhase();

        // Assert
        currentPhase.Should().NotBeNull();
        currentPhase!.PhaseNumber.Should().Be(1);
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenNameEmpty()
    {
        // Arrange & Act
        var act = () => Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "",  // Empty name
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Rollout name cannot be empty*");
    }

    // Helper methods

    private Rollout CreateTestRollout()
    {
        var rolloutId = Guid.NewGuid();
        var rollout = Rollout.Create(
            rolloutId,
            _tenantId,
            _bundleId,
            _targetVersion,
            _previousVersion,
            "Test Rollout",
            "Test rollout description",
            _deviceGroupId,
            "test@example.com",
            0.05m,
            DateTimeOffset.UtcNow);

        // Add phases after creation
        var phase1 = CreatePhase(rolloutId, 1, "Canary", 10m);
        var phase2 = CreatePhase(rolloutId, 2, "Production", 100m);

        rollout.AddPhase(phase1);
        rollout.AddPhase(phase2);

        return rollout;
    }

    private RolloutPhase CreatePhase(Guid rolloutId, int phaseNumber, string name, decimal targetPercentage)
    {
        return RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            phaseNumber,
            name,
            targetDeviceCount: 10,
            targetPercentage: targetPercentage,
            minHealthyDuration: TimeSpan.FromMinutes(5));
    }
}
