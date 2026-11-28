using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Integration;

// Operator endpoints act only within the caller's tenant (#498): another tenant's resources read as
// 404, lists never include them, and a caller without a tenant is refused.
[Trait("Category", "Integration")]
public class TenantScopingTests : IClassFixture<BundleOrchestratorFactory>
{
    private readonly BundleOrchestratorFactory _factory;
    private readonly HttpClient _a;
    private readonly HttpClient _b;

    public TenantScopingTests(BundleOrchestratorFactory factory)
    {
        _factory = factory;
        _a = factory.Client(BundleOrchestratorFactory.TenantAKey);
        _b = factory.Client(BundleOrchestratorFactory.TenantBKey);
    }

    [Fact]
    public async Task Bundle_OfAnotherTenant_IsNotFound()
    {
        // Arrange
        var bundleId = await CreateBundleAsync(_a);

        // Act and assert: every bundle and bundle-version route.
        foreach (var path in new[]
                 {
                     $"/api/bundles/{bundleId}",
                     $"/api/bundles/{bundleId}/assigned-devices",
                     $"/api/bundles/{bundleId}/versions/latest",
                     $"/api/bundles/{bundleId}/versions/1.0.0",
                     $"/api/bundles/{bundleId}/versions/1.0.0/download",
                     $"/api/rollouts/bundles/{bundleId}",
                     $"/api/phased-rollouts/bundles/{bundleId}/history"
                 })
        {
            (await _b.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound, path);
        }

        var newVersion = await _b.PostAsJsonAsync($"/api/bundles/{bundleId}/versions",
            new { version = "2.0.0", containers = new[] { new { name = "app", image = "nginx:1.27" } } });
        newVersion.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _a.GetAsync($"/api/bundles/{bundleId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BundleList_IgnoresTheTenantIdParameter()
    {
        // Arrange
        var bundleId = await CreateBundleAsync(_a);

        // Act
        var response = await _b.GetAsync($"/api/bundles?tenantId={BundleOrchestratorFactory.TenantA}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(bundleId.ToString());
    }

    [Fact]
    public async Task CreateBundle_WithAnotherTenantsId_IsCreatedInTheCallersTenant()
    {
        // Act
        var bundleId = await CreateBundleAsync(_b, tenantIdParameter: BundleOrchestratorFactory.TenantA);

        // Assert
        (await _a.GetAsync($"/api/bundles/{bundleId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _b.GetAsync($"/api/bundles/{bundleId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assignment_CrossingTenants_IsNotFound()
    {
        // Arrange
        var bundleId = await CreateBundleAsync(_a);
        var deviceA = _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantA);
        var deviceB = _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantB);
        var groupB = _factory.DeviceManager.AddGroup(BundleOrchestratorFactory.TenantB, deviceB);
        var body = new { bundleId, version = "1.0.0" };

        // Act
        var otherTenantsBundle = await _b.PostAsJsonAsync($"/api/devices/{deviceB}/bundle", body);
        var otherTenantsDevice = await _a.PostAsJsonAsync($"/api/devices/{deviceB}/bundle", body);
        var otherTenantsGroup = await _a.PostAsJsonAsync($"/api/device-groups/{groupB}/bundle", body);
        var bundleToOtherGroup = await _b.PostAsJsonAsync($"/api/device-groups/{groupB}/bundle", body);
        var own = await _a.PostAsJsonAsync($"/api/devices/{deviceA}/bundle", body);

        // Assert
        otherTenantsBundle.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherTenantsDevice.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherTenantsGroup.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bundleToOtherGroup.StatusCode.Should().Be(HttpStatusCode.NotFound);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rollout_OfAnotherTenant_IsNotFoundAndNotListed()
    {
        // Arrange
        var bundleId = await CreateBundleAsync(_a);
        var deviceA = _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantA);
        var deviceB = _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantB);
        var created = await _a.PostAsJsonAsync("/api/rollouts",
            new { bundleId, version = "1.0.0", targetType = "device", targetIds = new[] { deviceA.ToString() } });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var rolloutId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rolloutId").GetGuid();
        Guid statusId;
        await using (var db = _factory.DbContext())
        {
            statusId = await db.RolloutStatuses.Where(s => s.RolloutId == rolloutId).Select(s => s.Id).SingleAsync();
        }

        // Act and assert
        (await _b.GetAsync($"/api/rollouts/{rolloutId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _b.GetAsync($"/api/rollouts/{rolloutId}/devices")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _b.PostAsync($"/api/rollouts/{rolloutId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _b.PutAsJsonAsync($"/api/rollouts/{statusId}/status", new { status = "Failed" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _a.PostAsJsonAsync("/api/rollouts",
                new { bundleId, version = "1.0.0", targetType = "device", targetIds = new[] { deviceB.ToString() } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await _b.GetStringAsync("/api/rollouts?pageSize=100")).Should().NotContain(rolloutId.ToString());
        var listA = await _a.GetFromJsonAsync<JsonElement>("/api/rollouts?pageSize=100");
        var mine = listA.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == rolloutId);
        mine.GetProperty("tenantId").GetGuid().Should().Be(BundleOrchestratorFactory.TenantA);
    }

    [Fact]
    public async Task PhasedRollout_OfAnotherTenant_IsNotFoundAndNotListed()
    {
        // Arrange
        var bundleId = await CreateBundleAsync(_a);
        var groupA = _factory.DeviceManager.AddGroup(
            BundleOrchestratorFactory.TenantA, _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantA));
        var created = await _a.PostAsJsonAsync("/api/phased-rollouts", PhasedRollout(bundleId, groupA, BundleOrchestratorFactory.TenantA));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var rolloutId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rolloutId").GetGuid();

        // Act and assert
        (await _b.GetAsync($"/api/phased-rollouts/{rolloutId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var action in new[] { "start", "pause", "resume", "advance", "rollback" })
        {
            (await _b.PostAsync($"/api/phased-rollouts/{rolloutId}/{action}", null))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, action);
        }

        (await _b.GetStringAsync($"/api/phased-rollouts?tenantId={BundleOrchestratorFactory.TenantA}&page=1&pageSize=100"))
            .Should().NotContain(rolloutId.ToString());
        (await _b.GetStringAsync($"/api/phased-rollouts/active?tenantId={BundleOrchestratorFactory.TenantA}"))
            .Should().NotContain(rolloutId.ToString());
        (await _a.PostAsync($"/api/phased-rollouts/{rolloutId}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePhasedRollout_ForAnotherTenantsBundle_IsNotFound()
    {
        // Arrange: the body names tenant A, but the caller is tenant B.
        var bundleId = await CreateBundleAsync(_a);
        var groupA = _factory.DeviceManager.AddGroup(
            BundleOrchestratorFactory.TenantA, _factory.DeviceManager.AddDevice(BundleOrchestratorFactory.TenantA));

        // Act
        var response = await _b.PostAsJsonAsync("/api/phased-rollouts", PhasedRollout(bundleId, groupA, BundleOrchestratorFactory.TenantA));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET", "/api/bundles")]
    [InlineData("GET", "/api/rollouts")]
    [InlineData("GET", "/api/phased-rollouts/active")]
    [InlineData("POST", "/api/rollouts/3f0b4c2e-8f8e-4c5e-9a55-7b6f4f0a1c11/cancel")]
    public async Task CallerWithoutTenant_IsForbidden(string method, string path)
    {
        // Act
        var response = await _factory.Client(BundleOrchestratorFactory.NoTenantKey)
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("TENANT_REQUIRED");
    }

    private static async Task<Guid> CreateBundleAsync(HttpClient client, Guid? tenantIdParameter = null)
    {
        var path = tenantIdParameter is { } tenantId ? $"/api/bundles?tenantId={tenantId}" : "/api/bundles";
        var response = await client.PostAsJsonAsync(path, new
        {
            name = $"app-{Guid.NewGuid():N}"[..20],
            version = "1.0.0",
            containers = new[] { new { name = "app", image = "nginx", tag = "1.27" } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bundleId").GetGuid();
    }

    private static object PhasedRollout(Guid bundleId, Guid groupId, Guid tenantId) => new
    {
        tenantId,
        bundleId = bundleId.ToString(),
        targetVersion = "1.0.0",
        name = $"rollout-{Guid.NewGuid():N}"[..20],
        targetDeviceGroupId = groupId,
        phases = new[] { new { name = "all", targetPercentage = 100 } }
    };
}
