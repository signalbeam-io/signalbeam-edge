using Microsoft.Extensions.Logging;
using NSubstitute;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Results;
using SignalBeam.TelemetryProcessor.Application.Repositories;
using SignalBeam.TelemetryProcessor.Application.Services;
using SignalBeam.TelemetryProcessor.Infrastructure.Services;

namespace SignalBeam.TelemetryProcessor.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for DataRetentionService.
/// </summary>
public class DataRetentionServiceTests
{
    private readonly ITenantRetentionClient _tenantClient;
    private readonly IDeviceClient _deviceClient;
    private readonly IDeviceMetricsRepository _metricsRepository;
    private readonly IDeviceHeartbeatRepository _heartbeatRepository;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly DataRetentionService _service;

    public DataRetentionServiceTests()
    {
        _tenantClient = Substitute.For<ITenantRetentionClient>();
        _deviceClient = Substitute.For<IDeviceClient>();
        _metricsRepository = Substitute.For<IDeviceMetricsRepository>();
        _heartbeatRepository = Substitute.For<IDeviceHeartbeatRepository>();
        _logger = Substitute.For<ILogger<DataRetentionService>>();

        _service = new DataRetentionService(
            _tenantClient,
            _deviceClient,
            _metricsRepository,
            _heartbeatRepository,
            _logger);
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldReturnSuccess_WhenNoTenantsExist()
    {
        // Arrange
        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(
                new List<TenantRetentionInfo>()));

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantsProcessed.Should().Be(0);
        result.Value.MetricsDeleted.Should().Be(0);
        result.Value.HeartbeatsDeleted.Should().Be(0);
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldReturnFailure_WhenTenantClientFails()
    {
        // Arrange
        var error = Error.Failure("CLIENT_ERROR", "Failed to fetch tenants");
        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyCollection<TenantRetentionInfo>>(error));

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CLIENT_ERROR");
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldDeleteOldData_ForSingleTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var deviceId1 = new DeviceId(Guid.NewGuid());
        var deviceId2 = new DeviceId(Guid.NewGuid());

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenantId, "Test Tenant", 7)
        };

        var deviceIds = new List<DeviceId> { deviceId1, deviceId2 };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenantId),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(deviceIds));

        _metricsRepository.DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(150);

        _heartbeatRepository.DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(300);

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantsProcessed.Should().Be(1);
        result.Value.MetricsDeleted.Should().Be(150);
        result.Value.HeartbeatsDeleted.Should().Be(300);

        // Verify repositories were called with correct device IDs
        await _metricsRepository.Received(1).DeleteOldMetricsAsync(
            Arg.Is<IEnumerable<DeviceId>>(ids => ids.Count() == 2),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        await _heartbeatRepository.Received(1).DeleteOldHeartbeatsAsync(
            Arg.Is<IEnumerable<DeviceId>>(ids => ids.Count() == 2),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldDeleteOldData_ForMultipleTenants()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenant1Id, "Tenant 1", 7),
            new TenantRetentionInfo(tenant2Id, "Tenant 2", 90)
        };

        var devices1 = new List<DeviceId> { new DeviceId(Guid.NewGuid()) };
        var devices2 = new List<DeviceId> { new DeviceId(Guid.NewGuid()), new DeviceId(Guid.NewGuid()) };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant1Id),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(devices1));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant2Id),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(devices2));

        _metricsRepository.DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(100, 200);

        _heartbeatRepository.DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(150, 250);

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantsProcessed.Should().Be(2);
        result.Value.MetricsDeleted.Should().Be(300); // 100 + 200
        result.Value.HeartbeatsDeleted.Should().Be(400); // 150 + 250

        // Verify both tenants were processed
        await _deviceClient.Received(1).GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant1Id),
            Arg.Any<CancellationToken>());

        await _deviceClient.Received(1).GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant2Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldSkipTenant_WhenNoDevicesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenantId, "Test Tenant", 7)
        };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Any<TenantId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(new List<DeviceId>()));

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantsProcessed.Should().Be(1); // Tenant is counted even if skipped
        result.Value.MetricsDeleted.Should().Be(0);
        result.Value.HeartbeatsDeleted.Should().Be(0);

        // Verify deletion methods were not called
        await _metricsRepository.DidNotReceive().DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        await _heartbeatRepository.DidNotReceive().DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldContinueProcessing_WhenDeviceClientFailsForOneTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenant1Id, "Tenant 1", 7),
            new TenantRetentionInfo(tenant2Id, "Tenant 2", 90)
        };

        var devices2 = new List<DeviceId> { new DeviceId(Guid.NewGuid()) };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        // Tenant 1 fails to fetch devices
        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant1Id),
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyCollection<DeviceId>>(
                Error.Failure("DEVICE_FETCH_FAILED", "Failed to fetch devices")));

        // Tenant 2 succeeds
        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Is<TenantId>(t => t.Value == tenant2Id),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(devices2));

        _metricsRepository.DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(50);

        _heartbeatRepository.DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(100);

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantsProcessed.Should().Be(1); // Only successful tenant
        result.Value.MetricsDeleted.Should().Be(50);
        result.Value.HeartbeatsDeleted.Should().Be(100);
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldCalculateCorrectCutoffDate_ForFreeTier()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var deviceId = new DeviceId(Guid.NewGuid());
        var retentionDays = 7; // Free tier

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenantId, "Free Tenant", retentionDays)
        };

        var deviceIds = new List<DeviceId> { deviceId };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Any<TenantId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(deviceIds));

        _metricsRepository.DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(10);

        _heartbeatRepository.DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(20);

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify cutoff date is approximately 7 days ago
        await _metricsRepository.Received(1).DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Is<DateTimeOffset>(date =>
                date > DateTimeOffset.UtcNow.AddDays(-retentionDays - 1) &&
                date < DateTimeOffset.UtcNow.AddDays(-retentionDays + 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceDataRetentionAsync_ShouldCalculateCorrectCutoffDate_ForPaidTier()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var deviceId = new DeviceId(Guid.NewGuid());
        var retentionDays = 90; // Paid tier

        var tenants = new List<TenantRetentionInfo>
        {
            new TenantRetentionInfo(tenantId, "Paid Tenant", retentionDays)
        };

        var deviceIds = new List<DeviceId> { deviceId };

        _tenantClient.GetAllTenantsWithRetentionAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<TenantRetentionInfo>>(tenants));

        _deviceClient.GetDeviceIdsByTenantAsync(
            Arg.Any<TenantId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DeviceId>>(deviceIds));

        _metricsRepository.DeleteOldMetricsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(10);

        _heartbeatRepository.DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(20);

        // Act
        var result = await _service.EnforceDataRetentionAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify cutoff date is approximately 90 days ago
        await _heartbeatRepository.Received(1).DeleteOldHeartbeatsAsync(
            Arg.Any<IEnumerable<DeviceId>>(),
            Arg.Is<DateTimeOffset>(date =>
                date > DateTimeOffset.UtcNow.AddDays(-retentionDays - 1) &&
                date < DateTimeOffset.UtcNow.AddDays(-retentionDays + 1)),
            Arg.Any<CancellationToken>());
    }
}
