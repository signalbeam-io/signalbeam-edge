using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.BundleOrchestrator.Application.Services;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Commands;

// Group-targeted rollouts and assignments read the group's members from DeviceManager (#491),
// always within the tenant that owns the bundle or rollout.
public class DeviceGroupTargetingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid[] Devices = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    private readonly IRolloutRepository _rolloutRepository = Substitute.For<IRolloutRepository>();
    private readonly IBundleRepository _bundleRepository = Substitute.For<IBundleRepository>();
    private readonly IBundleVersionRepository _bundleVersionRepository = Substitute.For<IBundleVersionRepository>();
    private readonly IDeviceDesiredStateRepository _desiredStateRepository = Substitute.For<IDeviceDesiredStateRepository>();
    private readonly IRolloutStatusRepository _rolloutStatusRepository = Substitute.For<IRolloutStatusRepository>();
    private readonly IDeviceGroupClient _groupClient = Substitute.For<IDeviceGroupClient>();
    private readonly AppBundle _bundle = AppBundle.Create(new BundleId(Guid.NewGuid()), new TenantId(Tenant), "app", null, DateTimeOffset.UtcNow);

    public DeviceGroupTargetingTests()
    {
        _bundleRepository.GetByIdAsync(_bundle.Id, Arg.Any<CancellationToken>()).Returns(_bundle);
        _bundleVersionRepository
            .GetByBundleAndVersionAsync(_bundle.Id, Arg.Any<BundleVersion>(), Arg.Any<CancellationToken>())
            .Returns(AppBundleVersion.Create(Guid.NewGuid(), _bundle.Id, BundleVersion.Parse("1.0.0"),
                [ContainerSpec.Create("app", "nginx:1.27")], null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task CreatePhasedRollout_ForAGroup_SizesPhasesFromItsMembers()
    {
        // Arrange
        GroupReturns(DeviceGroupLookup.Found(new DeviceGroupMembers(GroupId, "fleet", Devices)));

        // Act
        var result = await PhasedRolloutHandler().Handle(PhasedRolloutCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _groupClient.Received(1).GetAsync(Tenant, GroupId, Arg.Any<CancellationToken>());
        await _rolloutRepository.Received(1).AddAsync(
            Arg.Is<Rollout>(r => r.Phases.Select(p => p.TargetDeviceCount).SequenceEqual(new[] { 1, 3 })),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DeviceGroupLookupStatus.NotFound, ErrorType.NotFound)]
    [InlineData(DeviceGroupLookupStatus.Unavailable, ErrorType.Failure)]
    public async Task CreatePhasedRollout_GroupNotLoaded_FailsWithoutSaving(DeviceGroupLookupStatus status, ErrorType expected)
    {
        // Arrange
        GroupReturns(new DeviceGroupLookup(status, null));

        // Act
        var result = await PhasedRolloutHandler().Handle(PhasedRolloutCommand(), CancellationToken.None);

        // Assert
        result.Error!.Type.Should().Be(expected);
        await _rolloutRepository.DidNotReceive().AddAsync(Arg.Any<Rollout>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRollout_AssignsTheFirstPhaseToTheEarliestMembers()
    {
        // Arrange
        var rollout = TwoPhaseRollout();
        _rolloutRepository.GetByIdAsync(rollout.Id, Arg.Any<CancellationToken>()).Returns(rollout);
        GroupReturns(DeviceGroupLookup.Found(new DeviceGroupMembers(GroupId, "fleet", Devices)));

        // Act
        var result = await new StartRolloutHandler(_rolloutRepository, _groupClient, _desiredStateRepository)
            .Handle(new StartRolloutCommand(rollout.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _groupClient.Received(1).GetAsync(Tenant, GroupId, Arg.Any<CancellationToken>());
        rollout.Phases.First().DeviceAssignments.Select(a => a.DeviceId.Value).Should().Equal(Devices[0]);
    }

    [Fact]
    public async Task AdvancePhase_AssignsTheNextMembersNotYetInTheRollout()
    {
        // Arrange: the canary phase already has the first member.
        var rollout = TwoPhaseRollout();
        rollout.Start(DateTimeOffset.UtcNow);
        rollout.StartCurrentPhase(DateTimeOffset.UtcNow);
        var canary = rollout.GetCurrentPhase()!;
        canary.AddDeviceAssignment(RolloutDeviceAssignment.Create(Guid.NewGuid(), rollout.Id, canary.Id, new DeviceId(Devices[0])));
        _rolloutRepository.GetByIdAsync(rollout.Id, Arg.Any<CancellationToken>()).Returns(rollout);
        GroupReturns(DeviceGroupLookup.Found(new DeviceGroupMembers(GroupId, "fleet", Devices)));

        // Act
        var result = await new AdvancePhaseHandler(_rolloutRepository, _groupClient, _desiredStateRepository)
            .Handle(new AdvancePhaseCommand(rollout.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _groupClient.Received(1).GetAsync(Tenant, GroupId, Arg.Any<CancellationToken>());
        rollout.GetCurrentPhase()!.DeviceAssignments.Select(a => a.DeviceId.Value).Should().Equal(Devices[1], Devices[2]);
    }

    [Theory]
    [InlineData(DeviceGroupLookupStatus.NotFound, ErrorType.NotFound)]
    [InlineData(DeviceGroupLookupStatus.Unavailable, ErrorType.Failure)]
    public async Task CreateRollout_ForAGroupNotLoaded_AssignsNothing(DeviceGroupLookupStatus status, ErrorType expected)
    {
        // Arrange
        GroupReturns(new DeviceGroupLookup(status, null));
        var handler = new CreateRolloutHandler(_bundleRepository, _bundleVersionRepository, _groupClient,
            _desiredStateRepository, _rolloutStatusRepository, new DeviceOwnershipVerifier(Substitute.For<IDeviceTenantClient>()));

        // Act
        var result = await handler.Handle(
            new CreateRolloutCommand(_bundle.Id.Value.ToString(), "1.0.0", "group", [GroupId.ToString()]),
            Tenant,
            CancellationToken.None);

        // Assert
        result.Error!.Type.Should().Be(expected);
        await _groupClient.Received(1).GetAsync(Tenant, GroupId, Arg.Any<CancellationToken>());
        await _desiredStateRepository.DidNotReceive().AddAsync(Arg.Any<DeviceDesiredState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignBundleToGroup_SetsDesiredStateForEveryMember()
    {
        // Arrange
        GroupReturns(DeviceGroupLookup.Found(new DeviceGroupMembers(GroupId, "fleet", Devices)));

        // Act
        var result = await AssignHandler().Handle(AssignCommand(), Tenant, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DeviceCount.Should().Be(3);
        await _groupClient.Received(1).GetAsync(Tenant, GroupId, Arg.Any<CancellationToken>());
        await _desiredStateRepository.Received(3).AddAsync(Arg.Any<DeviceDesiredState>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DeviceGroupLookupStatus.NotFound, ErrorType.NotFound)]
    [InlineData(DeviceGroupLookupStatus.Unavailable, ErrorType.Failure)]
    public async Task AssignBundleToGroup_GroupNotLoaded_AssignsNothing(DeviceGroupLookupStatus status, ErrorType expected)
    {
        // Arrange: NotFound also covers a group of another tenant.
        GroupReturns(new DeviceGroupLookup(status, null));

        // Act
        var result = await AssignHandler().Handle(AssignCommand(), Tenant, CancellationToken.None);

        // Assert
        result.Error!.Type.Should().Be(expected);
        await _desiredStateRepository.DidNotReceive().AddAsync(Arg.Any<DeviceDesiredState>(), Arg.Any<CancellationToken>());
    }

    // Canary takes 1 device, the second phase the other 2.
    private Rollout TwoPhaseRollout()
    {
        var rollout = Rollout.Create(Guid.NewGuid(), new TenantId(Tenant), _bundle.Id, BundleVersion.Parse("1.0.0"), null,
            "canary", null, GroupId, null, 0.05m, DateTimeOffset.UtcNow);
        rollout.AddPhase(RolloutPhase.Create(Guid.NewGuid(), rollout.Id, 0, "canary", 1, 10m, null));
        rollout.AddPhase(RolloutPhase.Create(Guid.NewGuid(), rollout.Id, 1, "fleet", 2, 90m, null));
        return rollout;
    }

    private void GroupReturns(DeviceGroupLookup lookup) =>
        _groupClient.GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(lookup);

    private CreatePhasedRolloutHandler PhasedRolloutHandler() =>
        new(_rolloutRepository, _bundleRepository, _bundleVersionRepository, _groupClient);

    private CreatePhasedRolloutCommand PhasedRolloutCommand() => new(
        Tenant, _bundle.Id.Value.ToString(), "1.0.0", null, "canary", null, GroupId,
        [new PhaseConfigDto("canary", 10m), new PhaseConfigDto("fleet", 90m)]);

    private AssignBundleToGroupHandler AssignHandler() =>
        new(_bundleRepository, _bundleVersionRepository, _groupClient, _desiredStateRepository, _rolloutStatusRepository);

    private AssignBundleToGroupCommand AssignCommand() => new(GroupId.ToString(), _bundle.Id.Value.ToString(), "1.0.0");
}
