using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Infrastructure.BackgroundServices;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Verifies the stale-Pending prune (#438): abandoned Pending devices past the TTL are hard-deleted;
/// recent Pending devices and non-Pending devices survive.
/// </summary>
public class PendingDevicePruneTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;

    public PendingDevicePruneTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private PendingDevicePruneService CreateService(double expiryHours = 24) =>
        new(
            NullLogger<PendingDevicePruneService>.Instance,
            _factory.Services,
            Options.Create(new PendingDevicePruneOptions { PendingDeviceExpiryHours = expiryHours }));

    private async Task<DeviceId> SeedDeviceAsync(DateTimeOffset registeredAt, bool approved = false)
    {
        var device = Device.Register(
            DeviceId.New(),
            new TenantId(_factory.DefaultTenantId),
            $"prune-test-{Guid.NewGuid():N}",
            registeredAt);

        if (approved)
        {
            device.ApproveRegistration(registeredAt);
        }

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private async Task<bool> DeviceExistsAsync(DeviceId deviceId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
        return await context.Devices.AnyAsync(d => d.Id == deviceId);
    }

    [Fact]
    public async Task PruneOnce_DeletesPendingDevicesOlderThanTtl()
    {
        var stale1 = await SeedDeviceAsync(DateTimeOffset.UtcNow.AddHours(-48));
        var stale2 = await SeedDeviceAsync(DateTimeOffset.UtcNow.AddHours(-25));

        var pruned = await CreateService(expiryHours: 24).PruneOnceAsync(CancellationToken.None);

        pruned.Should().BeGreaterThanOrEqualTo(2);
        (await DeviceExistsAsync(stale1)).Should().BeFalse();
        (await DeviceExistsAsync(stale2)).Should().BeFalse();
    }

    [Fact]
    public async Task PruneOnce_KeepsPendingDevicesWithinTtl()
    {
        var fresh = await SeedDeviceAsync(DateTimeOffset.UtcNow.AddHours(-1));

        await CreateService(expiryHours: 24).PruneOnceAsync(CancellationToken.None);

        (await DeviceExistsAsync(fresh)).Should().BeTrue();
    }

    [Fact]
    public async Task PruneOnce_NeverDeletesApprovedDevices()
    {
        var oldApproved = await SeedDeviceAsync(DateTimeOffset.UtcNow.AddDays(-30), approved: true);

        await CreateService(expiryHours: 24).PruneOnceAsync(CancellationToken.None);

        (await DeviceExistsAsync(oldApproved)).Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
        var device = await context.Devices.SingleAsync(d => d.Id == oldApproved);
        device.RegistrationStatus.Should().Be(DeviceRegistrationStatus.Approved);
    }
}
