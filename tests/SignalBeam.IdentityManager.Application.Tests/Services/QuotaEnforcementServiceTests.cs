using Microsoft.Extensions.Logging;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.IdentityManager.Infrastructure.Services;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Tests.Services;

public class QuotaEnforcementServiceTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<QuotaEnforcementService> _logger;
    private readonly QuotaEnforcementService _service;

    public QuotaEnforcementServiceTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        _logger = Substitute.For<ILogger<QuotaEnforcementService>>();

        _service = new QuotaEnforcementService(
            _tenantRepository,
            _subscriptionRepository,
            _logger);
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldSucceed_WhenUnderQuota()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        // Free tier: max 5 devices, current count: 3
        subscription.IncrementDeviceCount();
        subscription.IncrementDeviceCount();
        subscription.IncrementDeviceCount();

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldFail_WhenQuotaExceeded()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        // Free tier: max 5 devices, current count: 5 (at limit)
        for (int i = 0; i < 5; i++)
        {
            subscription.IncrementDeviceCount();
        }

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("DEVICE_QUOTA_EXCEEDED");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("5 devices");
        result.Error.Message.Should().Contain("upgrade");
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldSucceed_ForPaidTierWithManyDevices()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Paid, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Paid, DateTimeOffset.UtcNow);

        // Paid tier: unlimited devices, add 1000 devices
        for (int i = 0; i < 1000; i++)
        {
            subscription.IncrementDeviceCount();
        }

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldFail_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = TenantId.New();

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldFail_WhenNoActiveSubscription()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SUBSCRIPTION_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(0, true)]  // No devices, should pass
    [InlineData(1, true)]  // 1 device, should pass
    [InlineData(4, true)]  // 4 devices, should pass (can still add one more)
    [InlineData(5, false)] // 5 devices (at limit), should fail (cannot add more)
    public async Task CheckDeviceQuotaAsync_ShouldRespectFreeTierLimit(int currentDeviceCount, bool shouldSucceed)
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        for (int i = 0; i < currentDeviceCount; i++)
        {
            subscription.IncrementDeviceCount();
        }

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var result = await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        result.IsSuccess.Should().Be(shouldSucceed);
        if (!shouldSucceed)
        {
            result.Error!.Code.Should().Be("DEVICE_QUOTA_EXCEEDED");
        }
    }

    [Fact]
    public async Task GetCurrentDeviceCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var tenantId = TenantId.New();
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        subscription.IncrementDeviceCount();
        subscription.IncrementDeviceCount();
        subscription.IncrementDeviceCount();

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        var count = await _service.GetCurrentDeviceCountAsync(tenantId);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCurrentDeviceCountAsync_ShouldReturnZero_WhenNoActiveSubscription()
    {
        // Arrange
        var tenantId = TenantId.New();

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        // Act
        var count = await _service.GetCurrentDeviceCountAsync(tenantId);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldSucceed_WhenTenantExists()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        var result = await _service.EnforceDataRetentionAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldFail_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = TenantId.New();

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _service.EnforceDataRetentionAsync(tenantId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldLogWarning_WhenTenantNotFound()
    {
        // Arrange
        var tenantId = TenantId.New();

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(tenantId.Value.ToString())),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldLogInfo_WhenQuotaExceeded()
    {
        // Arrange
        var tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "Test Tenant", "test-tenant", SubscriptionTier.Free, DateTimeOffset.UtcNow);
        var subscription = Subscription.Create(Guid.NewGuid(), tenantId, SubscriptionTier.Free, DateTimeOffset.UtcNow);

        for (int i = 0; i < 5; i++)
        {
            subscription.IncrementDeviceCount();
        }

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        _subscriptionRepository.GetActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        // Act
        await _service.CheckDeviceQuotaAsync(tenantId);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("quota exceeded")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CheckDeviceQuotaAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var tenantId = TenantId.New();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _tenantRepository.GetByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<Tenant?>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await _service.CheckDeviceQuotaAsync(tenantId, cts.Token));
    }
}
