using Microsoft.EntityFrameworkCore;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence;
using SignalBeam.BundleOrchestrator.Infrastructure.Persistence.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using Testcontainers.PostgreSql;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Persistence;

// Starting or advancing a rollout adds device assignments to a rollout loaded by the same context.
// Saving them used to throw DbUpdateConcurrencyException, found once group rollouts worked (#491).
[Trait("Category", "Integration")]
public class RolloutRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
        .WithDatabase("bundles_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = Context();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task UpdateAsync_LoadedRolloutWithNewAssignments_InsertsThem()
    {
        // Arrange
        var (rolloutId, deviceId) = await SeedRolloutAsync();

        // Act: what StartRolloutHandler does.
        await using (var context = Context())
        {
            var repository = new RolloutRepository(context);
            var rollout = (await repository.GetByIdAsync(rolloutId))!;
            rollout.Start(DateTimeOffset.UtcNow);
            rollout.StartCurrentPhase(DateTimeOffset.UtcNow);
            var phase = rollout.GetCurrentPhase()!;
            phase.AddDeviceAssignment(RolloutDeviceAssignment.Create(Guid.NewGuid(), rolloutId, phase.Id, deviceId));

            await repository.UpdateAsync(rollout);
            await repository.SaveChangesAsync();
        }

        // Assert
        await using var verify = Context();
        var saved = (await new RolloutRepository(verify).GetByIdAsync(rolloutId))!;
        saved.Phases.Single().DeviceAssignments.Should().ContainSingle().Which.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task UpdateAsync_DetachedRollout_DoesNotDuplicateItsAssignments()
    {
        // Arrange: a rollout with one saved assignment, loaded by another context.
        var (rolloutId, deviceId) = await SeedRolloutAsync();
        await using (var context = Context())
        {
            var repository = new RolloutRepository(context);
            var rollout = (await repository.GetByIdAsync(rolloutId))!;
            rollout.Start(DateTimeOffset.UtcNow);
            var phase = rollout.Phases.Single();
            phase.AddDeviceAssignment(RolloutDeviceAssignment.Create(Guid.NewGuid(), rolloutId, phase.Id, deviceId));
            await repository.UpdateAsync(rollout);
            await repository.SaveChangesAsync();
        }

        Rollout detached;
        await using (var context = Context())
        {
            detached = (await new RolloutRepository(context).GetByIdAsync(rolloutId))!;
        }

        // Act
        detached.Pause();
        await using (var context = Context())
        {
            var repository = new RolloutRepository(context);
            await repository.UpdateAsync(detached);
            await repository.SaveChangesAsync();
        }

        // Assert
        await using var verify = Context();
        var saved = (await new RolloutRepository(verify).GetByIdAsync(rolloutId))!;
        saved.Status.Should().Be(Domain.Enums.RolloutLifecycleStatus.Paused);
        saved.Phases.Single().DeviceAssignments.Should().ContainSingle();
    }

    private async Task<(Guid RolloutId, DeviceId DeviceId)> SeedRolloutAsync()
    {
        var rollout = Rollout.Create(Guid.NewGuid(), new TenantId(Guid.NewGuid()), new BundleId(Guid.NewGuid()),
            BundleVersion.Parse("1.0.0"), null, "canary", null, Guid.NewGuid(), null, 0.05m, DateTimeOffset.UtcNow);
        rollout.AddPhase(RolloutPhase.Create(Guid.NewGuid(), rollout.Id, 0, "all", 1, 100m, null));

        await using var context = Context();
        var repository = new RolloutRepository(context);
        await repository.AddAsync(rollout);
        await repository.SaveChangesAsync();
        return (rollout.Id, new DeviceId(Guid.NewGuid()));
    }

    private BundleDbContext Context() => new(new DbContextOptionsBuilder<BundleDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options);
}
