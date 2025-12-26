using FluentAssertions;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Entities;

public class RolloutDeviceAssignmentTests
{
    private readonly Guid _rolloutId = Guid.NewGuid();
    private readonly Guid _phaseId = Guid.NewGuid();
    private readonly DeviceId _deviceId = new(Guid.NewGuid());

    [Fact]
    public void Create_ShouldCreatePendingAssignment_WithValidInputs()
    {
        // Arrange & Act
        var assignment = RolloutDeviceAssignment.Create(
            Guid.NewGuid(),
            _rolloutId,
            _phaseId,
            _deviceId);

        // Assert
        assignment.RolloutId.Should().Be(_rolloutId);
        assignment.PhaseId.Should().Be(_phaseId);
        assignment.DeviceId.Should().Be(_deviceId);
        assignment.Status.Should().Be(DeviceAssignmentStatus.Pending);
        assignment.RetryCount.Should().Be(0);
        assignment.AssignedAt.Should().BeNull();
        assignment.ReconciledAt.Should().BeNull();
        assignment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAssigned_ShouldTransitionToPending_ToAssigned()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        var assignedAt = DateTimeOffset.UtcNow;

        // Act
        assignment.MarkAssigned(assignedAt);

        // Assert
        assignment.Status.Should().Be(DeviceAssignmentStatus.Assigned);
        assignment.AssignedAt.Should().Be(assignedAt);
    }

    [Fact]
    public void MarkAssigned_ShouldThrow_WhenNotPending()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow); // Now Assigned

        // Act
        var act = () => assignment.MarkAssigned(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as assigned when status is Assigned.");
    }

    [Fact]
    public void MarkReconciling_ShouldTransitionFromAssigned()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);

        // Act
        assignment.MarkReconciling();

        // Assert
        assignment.Status.Should().Be(DeviceAssignmentStatus.Reconciling);
    }

    [Fact]
    public void MarkReconciling_ShouldTransitionFromPending()
    {
        // Arrange
        var assignment = CreateTestAssignment();

        // Act
        assignment.MarkReconciling();

        // Assert
        assignment.Status.Should().Be(DeviceAssignmentStatus.Reconciling);
    }

    [Fact]
    public void MarkReconciling_ShouldThrow_WhenSucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.MarkReconciling();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as reconciling when status is Succeeded.");
    }

    [Fact]
    public void MarkReconciling_ShouldThrow_WhenFailed()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.MarkReconciling();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as reconciling when status is Failed.");
    }

    [Fact]
    public void MarkSucceeded_ShouldTransitionToSucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        var reconciledAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        assignment.MarkSucceeded(reconciledAt);

        // Assert
        assignment.Status.Should().Be(DeviceAssignmentStatus.Succeeded);
        assignment.ReconciledAt.Should().Be(reconciledAt);
        assignment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_ShouldThrow_WhenAlreadySucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as succeeded when status is Succeeded.");
    }

    [Fact]
    public void MarkSucceeded_ShouldThrow_WhenFailed()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as succeeded when status is Failed.");
    }

    [Fact]
    public void MarkFailed_ShouldTransitionToFailed()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        var errorMessage = "Container pull failed";
        var failedAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        assignment.MarkFailed(errorMessage, failedAt);

        // Assert
        assignment.Status.Should().Be(DeviceAssignmentStatus.Failed);
        assignment.ErrorMessage.Should().Be(errorMessage);
        assignment.ReconciledAt.Should().Be(failedAt);
    }

    [Fact]
    public void MarkFailed_ShouldThrow_WhenSucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.MarkFailed("Error", DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot mark as failed when already succeeded.");
    }

    [Fact]
    public void Retry_ShouldIncrementRetryCount_AndResetToPending()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);

        // Act
        assignment.Retry();

        // Assert
        assignment.RetryCount.Should().Be(1);
        assignment.Status.Should().Be(DeviceAssignmentStatus.Pending);
        assignment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Retry_ShouldIncrementRetryCount_Multiple()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);

        // Act
        assignment.Retry(); // RetryCount = 1
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error 2", DateTimeOffset.UtcNow);
        assignment.Retry(); // RetryCount = 2

        // Assert
        assignment.RetryCount.Should().Be(2);
        assignment.Status.Should().Be(DeviceAssignmentStatus.Pending);
    }

    [Fact]
    public void Retry_ShouldThrow_WhenSucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Act
        var act = () => assignment.Retry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot retry a succeeded assignment.");
    }

    [Fact]
    public void CanRetry_ShouldReturnTrue_WhenBelowMaxRetries()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);

        // Act
        var canRetry = assignment.CanRetry(maxRetries: 3);

        // Assert
        canRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_ShouldReturnFalse_WhenAtMaxRetries()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);
        assignment.Retry(); // RetryCount = 1
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);
        assignment.Retry(); // RetryCount = 2
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow);
        assignment.Retry(); // RetryCount = 3
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkFailed("Test error", DateTimeOffset.UtcNow); // Now Failed with RetryCount = 3

        // Act
        var canRetry = assignment.CanRetry(maxRetries: 3);

        // Assert
        canRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_ShouldReturnFalse_WhenNotFailed()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);

        // Act
        var canRetry = assignment.CanRetry(maxRetries: 3);

        // Assert
        canRetry.Should().BeFalse(); // Not Failed, so cannot retry
    }

    [Fact]
    public void CanRetry_ShouldReturnFalse_WhenSucceeded()
    {
        // Arrange
        var assignment = CreateTestAssignment();
        assignment.MarkAssigned(DateTimeOffset.UtcNow);
        assignment.MarkReconciling();
        assignment.MarkSucceeded(DateTimeOffset.UtcNow);

        // Act
        var canRetry = assignment.CanRetry(maxRetries: 3);

        // Assert
        canRetry.Should().BeFalse();
    }

    // Helper methods

    private RolloutDeviceAssignment CreateTestAssignment()
    {
        return RolloutDeviceAssignment.Create(
            Guid.NewGuid(),
            _rolloutId,
            _phaseId,
            _deviceId);
    }
}
