using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Application.Repositories;
using SignalBeam.DeviceManager.Application.Services;
using SignalBeam.DeviceManager.Infrastructure.Persistence.Repositories;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.Authentication;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.DeviceManager.Tests.Integration;

public class DeviceRegistrationIntegrationTests : IClassFixture<DeviceManagerTestFixture>
{
    private readonly DeviceManagerTestFixture _fixture;

    public DeviceRegistrationIntegrationTests(DeviceManagerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterDevice_ShouldPersistToDatabase_AndBeRetrievable()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DeviceRepository(context);
        var tokenRepository = new DeviceRegistrationTokenRepository(context);
        var tokenService = new RegistrationTokenService();
        var quotaValidator = Substitute.For<IDeviceQuotaValidator>();
        quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var registerHandler = new RegisterDeviceHandler(repository, tokenRepository, tokenService, quotaValidator);
        var queryHandler = new GetDeviceByIdHandler(repository);

        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var registerCommand = new RegisterDeviceCommand(
            TenantId: tenantId,
            DeviceId: deviceId,
            Name: "Integration Test Device",
            Metadata: "{\"location\":\"test-lab\",\"model\":\"RPi4\"}");

        // Act - Register device
        var registerResult = await registerHandler.Handle(registerCommand, CancellationToken.None);

        // Assert - Registration successful
        registerResult.IsSuccess.Should().BeTrue();
        registerResult.Value.Should().NotBeNull();
        registerResult.Value!.DeviceId.Should().Be(deviceId);

        // Act - Query device
        var query = new GetDeviceByIdQuery(deviceId);
        var queryResult = await queryHandler.Handle(query, CancellationToken.None);

        // Assert - Device is retrievable
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.Should().NotBeNull();
        queryResult.Value!.Id.Should().Be(deviceId);
        queryResult.Value.TenantId.Should().Be(tenantId);
        queryResult.Value.Name.Should().Be("Integration Test Device");
        queryResult.Value.Metadata.Should().Be("{\"location\":\"test-lab\",\"model\":\"RPi4\"}");
        queryResult.Value.Status.Should().Be(DeviceStatus.Registered.ToString());
    }

    [Fact]
    public async Task RecordHeartbeat_ShouldUpdateDeviceStatus_InDatabase()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DeviceRepository(context);
        var tokenRepository = new DeviceRegistrationTokenRepository(context);
        var tokenService = new RegistrationTokenService();
        var quotaValidator = Substitute.For<IDeviceQuotaValidator>();
        quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var registerHandler = new RegisterDeviceHandler(repository, tokenRepository, tokenService, quotaValidator);
        var heartbeatHandler = new RecordHeartbeatHandler(repository);

        var deviceId = Guid.NewGuid();

        // Register device first
        var registerCommand = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: deviceId,
            Name: "Heartbeat Test Device");

        await registerHandler.Handle(registerCommand, CancellationToken.None);

        // Act - Record heartbeat
        var heartbeatCommand = new RecordHeartbeatCommand(
            DeviceId: deviceId,
            Timestamp: DateTimeOffset.UtcNow);

        var heartbeatResult = await heartbeatHandler.Handle(heartbeatCommand, CancellationToken.None);

        // Assert - Heartbeat recorded
        heartbeatResult.IsSuccess.Should().BeTrue();

        // Verify in database
        var device = await context.Devices
            .FirstOrDefaultAsync(d => d.Id == new DeviceId(deviceId));

        device.Should().NotBeNull();
        device!.Status.Should().Be(DeviceStatus.Online);
        device.LastSeenAt.Should().NotBeNull();
        device.LastSeenAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateDevice_ShouldModifyExistingDevice_InDatabase()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DeviceRepository(context);
        var tokenRepository = new DeviceRegistrationTokenRepository(context);
        var tokenService = new RegistrationTokenService();
        var quotaValidator = Substitute.For<IDeviceQuotaValidator>();
        quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var registerHandler = new RegisterDeviceHandler(repository, tokenRepository, tokenService, quotaValidator);
        var updateHandler = new UpdateDeviceHandler(repository);

        var deviceId = Guid.NewGuid();

        // Register device
        var registerCommand = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: deviceId,
            Name: "Original Name");

        await registerHandler.Handle(registerCommand, CancellationToken.None);

        // Act - Update device
        var updateCommand = new UpdateDeviceCommand(
            DeviceId: deviceId,
            Name: "Updated Name",
            Metadata: "{\"updated\":\"true\"}");

        var updateResult = await updateHandler.Handle(updateCommand, CancellationToken.None);

        // Assert - Update successful
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("Updated Name");

        // Verify in database
        var device = await context.Devices
            .FirstOrDefaultAsync(d => d.Id == new DeviceId(deviceId));

        device.Should().NotBeNull();
        device!.Name.Should().Be("Updated Name");
        device.Metadata.Should().Be("{\"updated\":\"true\"}");
    }

    [Fact]
    public async Task AddDeviceTag_ShouldPersistTags_InDatabase()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var repository = new DeviceRepository(context);
        var tokenRepository = new DeviceRegistrationTokenRepository(context);
        var tokenService = new RegistrationTokenService();
        var quotaValidator = Substitute.For<IDeviceQuotaValidator>();
        quotaValidator.CheckDeviceQuotaAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var registerHandler = new RegisterDeviceHandler(repository, tokenRepository, tokenService, quotaValidator);
        var tagHandler = new AddDeviceTagHandler(repository);

        var deviceId = Guid.NewGuid();

        // Register device
        await registerHandler.Handle(
            new RegisterDeviceCommand(
                TenantId: Guid.NewGuid(),
                Name: "Tag Test Device",
                Metadata: null,
                DeviceId: deviceId),
            CancellationToken.None);

        // Act - Add tags
        await tagHandler.Handle(
            new AddDeviceTagCommand(deviceId, "production"),
            CancellationToken.None);

        await tagHandler.Handle(
            new AddDeviceTagCommand(deviceId, "rpi"),
            CancellationToken.None);

        // Assert - Verify in database
        var device = await context.Devices
            .FirstOrDefaultAsync(d => d.Id == new DeviceId(deviceId));

        device.Should().NotBeNull();
        device!.Tags.Should().Contain("production");
        device.Tags.Should().Contain("rpi");
        device.Tags.Should().HaveCount(2);
    }
}
