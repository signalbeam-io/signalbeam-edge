using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.BundleOrchestrator.Integration.Tests.Fixtures;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.BundleOrchestrator.Integration.Tests.Commands;

/// <summary>
/// Integration tests for rollout entity persistence and retrieval.
/// Tests the full stack from Domain → Repository → Database.
/// </summary>
[Collection("Database")]
public class RolloutCommandsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TenantId _tenantId;
    private BundleId _bundleId;
    private BundleVersion _targetVersion = null!; // Initialized in InitializeAsync

    public RolloutCommandsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.CleanDatabaseAsync();

        _tenantId = new TenantId(Guid.NewGuid());
        _bundleId = BundleId.New();
        _targetVersion = BundleVersion.Parse("1.0.0");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_ShouldPersistRolloutWithPhases()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        var rolloutId = Guid.NewGuid();
        var rollout = Rollout.Create(
            rolloutId,
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "Test Rollout",
            "Integration test rollout",
            null,
            "test@example.com",
            0.05m,
            DateTimeOffset.UtcNow);

        // Add phases
        var phase1 = RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            0,
            "Canary",
            targetDeviceCount: 10,
            targetPercentage: 10m,
            minHealthyDuration: TimeSpan.FromMinutes(5));

        var phase2 = RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            1,
            "Production",
            targetDeviceCount: 90,
            targetPercentage: 90m,
            minHealthyDuration: TimeSpan.FromMinutes(10));

        rollout.AddPhase(phase1);
        rollout.AddPhase(phase2);

        // Act
        await rolloutRepo.AddAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Assert
        var context = scope.ServiceProvider.GetRequiredService<BundleDbContext>();
        var persisted = await context.Rollouts.FindAsync(rolloutId);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Test Rollout");
        persisted.Phases.Should().HaveCount(2);
        persisted.Phases.First().Name.Should().Be("Canary");
        persisted.Phases.Last().Name.Should().Be("Production");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRetrieveRolloutWithPhases()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        var rolloutId = Guid.NewGuid();
        var rollout = Rollout.Create(
            rolloutId,
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "Retrieval Test",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            0,
            "Single Phase",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: null);

        rollout.AddPhase(phase);

        await rolloutRepo.AddAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Act
        var retrieved = await rolloutRepo.GetByIdAsync(rolloutId, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(rolloutId);
        retrieved.Name.Should().Be("Retrieval Test");
        retrieved.Phases.Should().HaveCount(1);
        retrieved.Phases.First().Name.Should().Be("Single Phase");
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistRolloutStateChanges()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        var rolloutId = Guid.NewGuid();
        var rollout = Rollout.Create(
            rolloutId,
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "State Change Test",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            0,
            "Phase 1",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: TimeSpan.FromMinutes(5));

        rollout.AddPhase(phase);

        await rolloutRepo.AddAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Act - Start rollout
        rollout.Start(DateTimeOffset.UtcNow);
        await rolloutRepo.UpdateAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Assert
        var context = scope.ServiceProvider.GetRequiredService<BundleDbContext>();
        var persisted = await context.Rollouts.FindAsync(rolloutId);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(RolloutLifecycleStatus.InProgress);
        persisted.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Rollout_LifecycleTransitions_ShouldPersist()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        var rolloutId = Guid.NewGuid();
        var previousVersion = BundleVersion.Parse("0.9.0"); // Add previous version for rollback
        var rollout = Rollout.Create(
            rolloutId,
            _tenantId,
            _bundleId,
            _targetVersion,
            previousVersion, // Must have previous version to rollback
            "Lifecycle Test",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            rolloutId,
            0,
            "Single Phase",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: null);

        rollout.AddPhase(phase);

        await rolloutRepo.AddAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Act - Lifecycle: Pending → InProgress → Paused → InProgress → RolledBack
        rollout.Start(DateTimeOffset.UtcNow);
        await rolloutRepo.UpdateAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        rollout.Pause();
        await rolloutRepo.UpdateAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        rollout.Resume(DateTimeOffset.UtcNow);
        await rolloutRepo.UpdateAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        rollout.Rollback(DateTimeOffset.UtcNow);
        await rolloutRepo.UpdateAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Assert
        var context = scope.ServiceProvider.GetRequiredService<BundleDbContext>();
        var persisted = await context.Rollouts.FindAsync(rolloutId);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(RolloutLifecycleStatus.RolledBack);
        persisted.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HasActiveRolloutAsync_ShouldReturnTrue_WhenActiveRolloutExists()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        var rollout = Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "Active Rollout",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase = RolloutPhase.Create(
            Guid.NewGuid(),
            rollout.Id,
            0,
            "Phase 1",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: null);

        rollout.AddPhase(phase);
        rollout.Start(DateTimeOffset.UtcNow); // Start the rollout to make it active

        await rolloutRepo.AddAsync(rollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Act
        var hasActive = await rolloutRepo.HasActiveRolloutAsync(_bundleId, CancellationToken.None);

        // Assert
        hasActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveRolloutsAsync_ShouldReturnInProgressRollouts()
    {
        // Arrange
        using var scope = _fixture.CreateScope();
        var rolloutRepo = scope.ServiceProvider.GetRequiredService<IRolloutRepository>();

        // Create two rollouts - one in progress, one completed
        var activeRollout = Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            _bundleId,
            _targetVersion,
            null,
            "Active Rollout",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase1 = RolloutPhase.Create(
            Guid.NewGuid(),
            activeRollout.Id,
            0,
            "Phase",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: null);

        activeRollout.AddPhase(phase1);
        activeRollout.Start(DateTimeOffset.UtcNow);

        var completedRollout = Rollout.Create(
            Guid.NewGuid(),
            _tenantId,
            BundleId.New(), // Different bundle
            _targetVersion,
            null,
            "Completed Rollout",
            null,
            null,
            null,
            0.05m,
            DateTimeOffset.UtcNow);

        var phase2 = RolloutPhase.Create(
            Guid.NewGuid(),
            completedRollout.Id,
            0,
            "Phase",
            targetDeviceCount: 100,
            targetPercentage: 100m,
            minHealthyDuration: null);

        completedRollout.AddPhase(phase2);
        completedRollout.Start(DateTimeOffset.UtcNow);
        completedRollout.StartCurrentPhase(DateTimeOffset.UtcNow);
        completedRollout.CompleteCurrentPhase(DateTimeOffset.UtcNow);

        await rolloutRepo.AddAsync(activeRollout, CancellationToken.None);
        await rolloutRepo.AddAsync(completedRollout, CancellationToken.None);
        await rolloutRepo.SaveChangesAsync(CancellationToken.None);

        // Act
        var activeRollouts = await rolloutRepo.GetActiveRolloutsAsync(_tenantId, CancellationToken.None);

        // Assert
        activeRollouts.Should().HaveCount(1);
        activeRollouts.First().Id.Should().Be(activeRollout.Id);
        activeRollouts.First().Status.Should().Be(RolloutLifecycleStatus.InProgress);
    }
}
