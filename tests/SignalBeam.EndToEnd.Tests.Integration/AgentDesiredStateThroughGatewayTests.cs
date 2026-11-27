using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Cloud;

namespace SignalBeam.EndToEnd.Tests.Integration;

// #489: the real agent client, sending its own device key, reaches BundleOrchestrator's device
// endpoints through the gateway; BundleOrchestrator verifies the key with DeviceManager.
[Trait("Category", "Integration")]
public class AgentDesiredStateThroughGatewayTests : IClassFixture<DeviceFlowFixture>
{
    private readonly DeviceFlowFixture _fixture;

    public AgentDesiredStateThroughGatewayTests(DeviceFlowFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Agent_FetchesItsDesiredState_WithContainers()
    {
        // Arrange
        var (deviceId, apiKey) = await OnboardDeviceAsync();
        var bundleId = await AssignBundleAsync(deviceId, "1.4.0");
        var agent = AgentClient(deviceId, apiKey);

        // Act
        var desired = await agent.FetchDesiredStateAsync(deviceId);

        // Assert
        desired.Should().NotBeNull();
        desired!.BundleId.Should().Be(bundleId.Value.ToString());
        desired.BundleVersion.Should().Be("1.4.0");
        var container = desired.Containers.Should().ContainSingle().Subject;
        container.Name.Should().Be("web");
        container.Image.Should().Be("nginx:1.27");
        container.Environment.Should().Equal(new Dictionary<string, string> { ["MODE"] = "edge" });
        container.Ports.Should().Equal(new Dictionary<string, string> { ["80/tcp"] = "8080" });
    }

    [Fact]
    public async Task Agent_WithNothingAssigned_GetsNoDesiredState()
    {
        // Arrange
        var (deviceId, apiKey) = await OnboardDeviceAsync();

        // Act: the agent turns the 404 into null and skips reconciliation, leaving containers alone.
        var desired = await AgentClient(deviceId, apiKey).FetchDesiredStateAsync(deviceId);

        // Assert
        desired.Should().BeNull();
    }

    [Fact]
    public async Task Agent_ReportsReconciliation_UpdatesItsRolloutStatus()
    {
        // Arrange
        var (deviceId, apiKey) = await OnboardDeviceAsync();
        var bundleId = await AssignBundleAsync(deviceId, "2.0.0");
        var rolloutStatusId = await StartRolloutAsync(deviceId, bundleId, "2.0.0");
        var agent = AgentClient(deviceId, apiKey);

        // Act
        await agent.ReportReconciliationStatusAsync(new ReconciliationStatus(
            deviceId, "succeeded", bundleId.Value.ToString(), "2.0.0", DateTime.UtcNow, [], []));

        // Assert
        RolloutState? state = null;
        await _fixture.WithBundleDbAsync(async db =>
            state = (await db.RolloutStatuses.AsNoTracking().SingleAsync(r => r.Id == rolloutStatusId)).Status);
        state.Should().Be(RolloutState.Succeeded);
    }

    [Theory]
    [InlineData("GET", "desired-state")]
    [InlineData("POST", "reconciliation-status")]
    public async Task DeviceKey_ForAnotherDevice_IsForbidden(string method, string endpoint)
    {
        // Arrange: both devices are in the same tenant; the key still names only its own device.
        var (_, apiKey) = await OnboardDeviceAsync();
        var (otherDeviceId, _) = await OnboardDeviceAsync();

        // Act
        using var response = await SendAsync(_fixture.CreateGatewayClient(apiKey), method, otherDeviceId, endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("sb_device_00000000_notarealkey", HttpStatusCode.Unauthorized)]
    public async Task MissingOrInvalidDeviceKey_IsUnauthorized(string? apiKey, HttpStatusCode expected)
    {
        // Arrange
        var (deviceId, _) = await OnboardDeviceAsync();

        // Act
        using var desiredState = await SendAsync(_fixture.CreateGatewayClient(apiKey), "GET", deviceId, "desired-state");
        using var reconciliation = await SendAsync(_fixture.CreateGatewayClient(apiKey), "POST", deviceId, "reconciliation-status");

        // Assert
        desiredState.StatusCode.Should().Be(expected);
        reconciliation.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task RevokedKey_IsRefusedOnceVerificationIsNoLongerCached()
    {
        // Arrange: a key that was never used here, then replaced with revocation before first use.
        var (deviceId, oldKey) = await OnboardDeviceAsync();
        using var rotate = await _fixture.CreateGatewayClient(DeviceFlowFixture.OperatorKey).PostAsJsonAsync(
            $"/api/devices/{deviceId}/api-keys", new { revokeExistingKeys = true });
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var response = await SendAsync(_fixture.CreateGatewayClient(oldKey), "GET", deviceId, "desired-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpCloudClient AgentClient(Guid deviceId, string apiKey)
    {
        var store = Substitute.For<IDeviceCredentialsStore>();
        store.LoadCredentialsAsync(Arg.Any<CancellationToken>()).Returns(new DeviceCredentials
        {
            DeviceId = deviceId,
            TenantId = _fixture.TenantId,
            ApiKey = apiKey
        });

        // The agent's own pipeline: DeviceApiKeyHandler adds X-API-Key, as in its DI setup.
        var handler = new DeviceApiKeyHandler(store, NullLogger<DeviceApiKeyHandler>.Instance)
        {
            InnerHandler = _fixture.CreateGatewayHandler()
        };
        return new HttpCloudClient(
            new HttpClient(handler) { BaseAddress = _fixture.GatewayAddress },
            NullLogger<HttpCloudClient>.Instance);
    }

    // Registers and approves a device through the gateway, as an operator would; returns its key.
    private async Task<(Guid DeviceId, string ApiKey)> OnboardDeviceAsync()
    {
        var deviceId = Guid.NewGuid();
        var operatorClient = _fixture.CreateGatewayClient(DeviceFlowFixture.OperatorKey);

        using var register = await operatorClient.PostAsJsonAsync("/api/devices",
            new { tenantId = _fixture.TenantId, deviceId, name = $"e2e-{deviceId:N}"[..20] });
        register.StatusCode.Should().Be(HttpStatusCode.Created, await register.Content.ReadAsStringAsync());

        using var approve = await operatorClient.PostAsync($"/api/devices/{deviceId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var body = await approve.Content.ReadFromJsonAsync<JsonElement>();
        return (deviceId, body.GetProperty("apiKey").GetString()!);
    }

    private async Task<BundleId> AssignBundleAsync(Guid deviceId, string version)
    {
        var bundleId = new BundleId(Guid.NewGuid());
        var bundleVersion = BundleVersion.Parse(version);
        await _fixture.WithBundleDbAsync(async db =>
        {
            db.AppBundles.Add(AppBundle.Create(bundleId, new TenantId(_fixture.TenantId), $"bundle-{bundleId.Value:N}"[..20], null, DateTimeOffset.UtcNow));
            db.AppBundleVersions.Add(AppBundleVersion.Create(
                Guid.NewGuid(), bundleId, bundleVersion,
                [SignalBeam.Domain.ValueObjects.ContainerSpec.Create("web", "nginx:1.27", "{\"MODE\":\"edge\"}", "[\"8080:80\"]")],
                null, DateTimeOffset.UtcNow));
            db.DeviceDesiredStates.Add(DeviceDesiredState.Create(
                Guid.NewGuid(), new DeviceId(deviceId), bundleId, bundleVersion, "e2e", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        return bundleId;
    }

    private async Task<Guid> StartRolloutAsync(Guid deviceId, BundleId bundleId, string version)
    {
        var rolloutStatus = RolloutStatus.Create(
            Guid.NewGuid(), Guid.NewGuid(), bundleId, BundleVersion.Parse(version), new DeviceId(deviceId), DateTimeOffset.UtcNow);
        await _fixture.WithBundleDbAsync(async db =>
        {
            db.RolloutStatuses.Add(rolloutStatus);
            await db.SaveChangesAsync();
        });
        return rolloutStatus.Id;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, Guid deviceId, string endpoint) =>
        method == "GET"
            ? client.GetAsync($"/api/devices/{deviceId}/{endpoint}")
            : client.PostAsJsonAsync($"/api/devices/{deviceId}/{endpoint}",
                new { deviceId, status = "succeeded", bundleId = (string?)null, bundleVersion = (string?)null });
}
