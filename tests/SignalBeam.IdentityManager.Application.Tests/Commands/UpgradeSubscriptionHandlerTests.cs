using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Tests.Commands;

public class UpgradeSubscriptionHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly UpgradeSubscriptionHandler _handler;

    public UpgradeSubscriptionHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _tenantRepository = Substitute.For<ITenantRepository>();
        _subscriptionRepository = Substitute.For<ISubscriptionRepository>();

        _handler = new UpgradeSubscriptionHandler(
            _tenantRepository,
            _subscriptionRepository,
            _userRepository);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUpgradingFromFreeToPaid()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OldTier.Should().Be(SubscriptionTier.Free);
        result.Value.NewTier.Should().Be(SubscriptionTier.Paid);
        result.Value.MaxDevices.Should().Be(int.MaxValue); // Paid tier has unlimited devices
        result.Value.DataRetentionDays.Should().Be(90); // Paid tier has 90 days retention

        // Verify tenant was updated
        await _tenantRepository.Received(1).UpdateAsync(
            Arg.Is<Tenant>(t => t.SubscriptionTier == SubscriptionTier.Paid),
            Arg.Any<CancellationToken>());

        // Verify subscription was updated
        await _subscriptionRepository.Received(1).UpdateAsync(
            Arg.Is<Subscription>(s => s.Tier == SubscriptionTier.Paid),
            Arg.Any<CancellationToken>());

        // Verify transaction committed
        await _tenantRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        var command = new UpgradeSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: Guid.NewGuid());

        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("USER_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotAdmin()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        // User is DeviceOwner, not Admin
        var user = User.Create(userId, tenantId, "user@test.com", "User", "zitadel-1", UserRole.DeviceOwner, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INSUFFICIENT_PERMISSIONS");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Message.Should().Contain("administrators");

        // Verify no updates were made
        await _tenantRepository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _subscriptionRepository.DidNotReceive().UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserTriesToUpgradeAnotherTenant()
    {
        // Arrange
        var userTenantId = TenantId.New();
        var targetTenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: targetTenantId.Value, // Different tenant
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        // User belongs to a different tenant
        var user = User.Create(userId, userTenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CROSS_TENANT_ACCESS");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Message.Should().Contain("another tenant");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTryingToDowngrade()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Free, // Trying to downgrade to Free
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Paid, DateTimeOffset.UtcNow); // Currently on Paid

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_TIER_CHANGE");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("downgrade");

        // Verify no updates were made
        await _tenantRepository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAlreadyOnSameTier()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid, // Already on Paid
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Paid, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SAME_TIER");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("already on this subscription tier");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNoActiveSubscription()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SUBSCRIPTION_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTenantQuotas_WhenUpgrading()
    {
        // Arrange
        var tenantId = TenantId.New();
        var userId = UserId.New();

        var command = new UpgradeSubscriptionCommand(
            TenantId: tenantId.Value,
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: userId.Value);

        var user = User.Create(userId, tenantId, "admin@test.com", "Admin", "zitadel-1", UserRole.Admin, DateTimeOffset.UtcNow);
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify tenant quotas were updated
        await _tenantRepository.Received(1).UpdateAsync(
            Arg.Is<Tenant>(t =>
                t.SubscriptionTier == SubscriptionTier.Paid &&
                t.MaxDevices == int.MaxValue &&
                t.DataRetentionDays == 90 &&
                t.UpgradedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRespectCancellationToken()
    {
        // Arrange
        var command = new UpgradeSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            NewTier: SubscriptionTier.Paid,
            UpgradedByUserId: Guid.NewGuid());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<User?>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await _handler.Handle(command, cts.Token));
    }
}
