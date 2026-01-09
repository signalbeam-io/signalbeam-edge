using Microsoft.Extensions.Logging;
using NSubstitute;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Queries;
using SignalBeam.IdentityManager.Application.Repositories;

namespace SignalBeam.IdentityManager.Application.Tests.Queries;

/// <summary>
/// Unit tests for GetTenantsWithRetentionHandler.
/// </summary>
public class GetTenantsWithRetentionHandlerTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<GetTenantsWithRetentionHandler> _logger;
    private readonly GetTenantsWithRetentionHandler _handler;

    public GetTenantsWithRetentionHandlerTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _logger = Substitute.For<ILogger<GetTenantsWithRetentionHandler>>();

        _handler = new GetTenantsWithRetentionHandler(_tenantRepository, _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenTenantsExist()
    {
        // Arrange
        var tenant1 = Tenant.Create(
            new TenantId(Guid.NewGuid()),
            "Tenant 1",
            "tenant-1",
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        var tenant2 = Tenant.Create(
            new TenantId(Guid.NewGuid()),
            "Tenant 2",
            "tenant-2",
            SubscriptionTier.Paid,
            DateTimeOffset.UtcNow);

        var tenants = new List<Tenant> { tenant1, tenant2 };

        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(tenants);

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(2);

        var tenant1Dto = result.Value.First(t => t.TenantId == tenant1.Id.Value);
        tenant1Dto.TenantName.Should().Be("Tenant 1");
        tenant1Dto.DataRetentionDays.Should().Be(7); // Free tier

        var tenant2Dto = result.Value.First(t => t.TenantId == tenant2.Id.Value);
        tenant2Dto.TenantName.Should().Be("Tenant 2");
        tenant2Dto.DataRetentionDays.Should().Be(90); // Paid tier
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoTenantsExist()
    {
        // Arrange
        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tenant>());

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsException()
    {
        // Arrange
        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns<List<Tenant>>(x => throw new InvalidOperationException("Database error"));

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_FETCH_FAILED");
        result.Error.Message.Should().Contain("Database error");
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectRetentionDays_ForFreeTier()
    {
        // Arrange
        var tenant = Tenant.Create(
            new TenantId(Guid.NewGuid()),
            "Free Tenant",
            "free-tenant",
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tenant> { tenant });

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.First();
        dto.DataRetentionDays.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectRetentionDays_ForPaidTier()
    {
        // Arrange
        var tenant = Tenant.Create(
            new TenantId(Guid.NewGuid()),
            "Paid Tenant",
            "paid-tenant",
            SubscriptionTier.Paid,
            DateTimeOffset.UtcNow);

        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tenant> { tenant });

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.First();
        dto.DataRetentionDays.Should().Be(90);
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnActiveTenants()
    {
        // Arrange
        // Repository should only return active tenants due to GetAllActiveAsync
        var activeTenant = Tenant.Create(
            new TenantId(Guid.NewGuid()),
            "Active Tenant",
            "active-tenant",
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        _tenantRepository.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Tenant> { activeTenant });

        var query = new GetTenantsWithRetentionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(1);
        result.Value.First().TenantName.Should().Be("Active Tenant");

        // Verify repository was called with correct method
        await _tenantRepository.Received(1).GetAllActiveAsync(Arg.Any<CancellationToken>());
    }
}
