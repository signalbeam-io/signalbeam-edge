using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Queries;
using SignalBeam.DeviceManager.Tests.Integration.Infrastructure;

namespace SignalBeam.DeviceManager.Tests.Integration;

/// <summary>
/// Integration tests for Tag CRUD operations.
/// </summary>
public class TagOperationsIntegrationTests : IClassFixture<DeviceManagerWebApplicationFactory>
{
    private readonly DeviceManagerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TagOperationsIntegrationTests(DeviceManagerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAuthenticatedClient();
    }

    #region Tag CRUD Operations

    [Fact]
    public async Task AddDeviceTag_WithValidKeyValueTag_AddsTagSuccessfully()
    {
        // Arrange - Create a device first
        var deviceId = await CreateTestDeviceAsync("tag-test-device-1");
        var tag = "environment=production";

        // Act - Add tag
        var addTagRequest = new AddDeviceTagCommand(deviceId, tag);
        var addResponse = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", addTagRequest);

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify tag was added
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device.Should().NotBeNull();
        device!.Tags.Should().Contain(tag);
    }

    [Fact]
    public async Task AddDeviceTag_WithSimpleTag_AddsTagSuccessfully()
    {
        // Arrange
        var deviceId = await CreateTestDeviceAsync("tag-test-device-2");
        var tag = "production";

        // Act
        var addTagRequest = new AddDeviceTagCommand(deviceId, tag);
        var addResponse = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", addTagRequest);

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify tag was added
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device!.Tags.Should().Contain(tag);
    }

    [Fact]
    public async Task AddDeviceTag_WithMultipleTags_AddsAllTagsSuccessfully()
    {
        // Arrange
        var deviceId = await CreateTestDeviceAsync("tag-test-device-3");
        var tags = new[] { "environment=production", "location=warehouse-1", "hardware=rpi4" };

        // Act - Add multiple tags
        foreach (var tag in tags)
        {
            var addTagRequest = new AddDeviceTagCommand(deviceId, tag);
            await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", addTagRequest);
        }

        // Assert
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device!.Tags.Should().Contain(tags);
    }

    [Fact]
    public async Task RemoveDeviceTag_WithExistingTag_RemovesTagSuccessfully()
    {
        // Arrange - Create device with tags
        var deviceId = await CreateTestDeviceAsync("tag-test-device-4");
        var tag = "environment=staging";
        var addTagRequest = new AddDeviceTagCommand(deviceId, tag);
        await _client.PostAsJsonAsync($"/api/devices/{deviceId}/tags", addTagRequest);

        // Act - Remove tag
        var removeResponse = await _client.DeleteAsync($"/api/tags/{deviceId}/tags/{tag}");

        // Assert
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify tag was removed
        var deviceResponse = await _client.GetAsync($"/api/devices/{deviceId}?tenantId={_factory.DefaultTenantId}");
        var device = await deviceResponse.Content.ReadFromJsonAsync<GetDeviceByIdResponse>();
        device!.Tags.Should().NotContain(tag);
    }

    [Fact]
    public async Task GetAllTags_WithMultipleDevicesWithTags_ReturnsAllUniqueTags()
    {
        // Arrange - Create multiple devices with different tags
        var device1Id = await CreateTestDeviceAsync("tag-test-device-5");
        var device2Id = await CreateTestDeviceAsync("tag-test-device-6");

        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "location=warehouse-1"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "hardware=rpi4"));

        // Act
        var response = await _client.GetAsync($"/api/tags?tenantId={_factory.DefaultTenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetAllTagsResponse>();
        result.Should().NotBeNull();
        result!.Tags.Should().Contain(t => t.Tag == "environment=production");
        result.Tags.Should().Contain(t => t.Tag == "location=warehouse-1");
        result.Tags.Should().Contain(t => t.Tag == "hardware=rpi4");
    }

    #endregion

    #region Tag Query Search

    [Fact]
    public async Task SearchDevicesByTagQuery_WithSimpleMatch_ReturnsMatchingDevices()
    {
        // Arrange - Create devices with different tags
        var prodDeviceId = await CreateTestDeviceAsync("search-test-prod");
        var stagingDeviceId = await CreateTestDeviceAsync("search-test-staging");

        await _client.PostAsJsonAsync($"/api/devices/{prodDeviceId}/tags", new AddDeviceTagCommand(prodDeviceId, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{stagingDeviceId}/tags", new AddDeviceTagCommand(stagingDeviceId, "environment=staging"));

        // Act - Search for production devices
        var query = "environment=production";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={query}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().Contain(d => d.Id == prodDeviceId);
        result.Devices.Should().NotContain(d => d.Id == stagingDeviceId);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithWildcardPattern_ReturnsMatchingDevices()
    {
        // Arrange
        var device1Id = await CreateTestDeviceAsync("search-test-warehouse-1");
        var device2Id = await CreateTestDeviceAsync("search-test-warehouse-2");
        var device3Id = await CreateTestDeviceAsync("search-test-office");

        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "location=warehouse-seattle"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "location=warehouse-portland"));
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "location=office-nyc"));

        // Act - Search with wildcard
        var query = "location=warehouse-*";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={Uri.EscapeDataString(query)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCount(2);
        result.Devices.Should().Contain(d => d.Id == device1Id);
        result.Devices.Should().Contain(d => d.Id == device2Id);
        result.Devices.Should().NotContain(d => d.Id == device3Id);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithAndOperator_ReturnsDevicesMatchingBothConditions()
    {
        // Arrange
        var device1Id = await CreateTestDeviceAsync("search-test-and-1");
        var device2Id = await CreateTestDeviceAsync("search-test-and-2");
        var device3Id = await CreateTestDeviceAsync("search-test-and-3");

        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "location=warehouse-1"));

        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "location=office-1"));

        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "environment=staging"));
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "location=warehouse-1"));

        // Act
        var query = "environment=production AND location=warehouse-1";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={Uri.EscapeDataString(query)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCount(1);
        result.Devices.Should().Contain(d => d.Id == device1Id);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithOrOperator_ReturnsDevicesMatchingEitherCondition()
    {
        // Arrange
        var device1Id = await CreateTestDeviceAsync("search-test-or-1");
        var device2Id = await CreateTestDeviceAsync("search-test-or-2");
        var device3Id = await CreateTestDeviceAsync("search-test-or-3");

        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "hardware=rpi4"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "hardware=rpi5"));
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "hardware=x86"));

        // Act
        var query = "hardware=rpi4 OR hardware=rpi5";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={Uri.EscapeDataString(query)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCount(2);
        result.Devices.Should().Contain(d => d.Id == device1Id);
        result.Devices.Should().Contain(d => d.Id == device2Id);
        result.Devices.Should().NotContain(d => d.Id == device3Id);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithNotOperator_ReturnsDevicesNotMatchingCondition()
    {
        // Arrange
        var device1Id = await CreateTestDeviceAsync("search-test-not-1");
        var device2Id = await CreateTestDeviceAsync("search-test-not-2");

        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "environment=dev"));

        // Act
        var query = "NOT environment=dev";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={Uri.EscapeDataString(query)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().Contain(d => d.Id == device1Id);
        result.Devices.Should().NotContain(d => d.Id == device2Id);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithComplexQuery_ReturnsCorrectDevices()
    {
        // Arrange - Real-world scenario: production or staging devices in warehouses
        var device1Id = await CreateTestDeviceAsync("complex-search-1");
        var device2Id = await CreateTestDeviceAsync("complex-search-2");
        var device3Id = await CreateTestDeviceAsync("complex-search-3");
        var device4Id = await CreateTestDeviceAsync("complex-search-4");

        // Production warehouse device
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device1Id}/tags", new AddDeviceTagCommand(device1Id, "location=warehouse-seattle"));

        // Staging warehouse device
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "environment=staging"));
        await _client.PostAsJsonAsync($"/api/devices/{device2Id}/tags", new AddDeviceTagCommand(device2Id, "location=warehouse-portland"));

        // Production office device (should not match)
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "environment=production"));
        await _client.PostAsJsonAsync($"/api/devices/{device3Id}/tags", new AddDeviceTagCommand(device3Id, "location=office-nyc"));

        // Dev warehouse device (should not match)
        await _client.PostAsJsonAsync($"/api/devices/{device4Id}/tags", new AddDeviceTagCommand(device4Id, "environment=dev"));
        await _client.PostAsJsonAsync($"/api/devices/{device4Id}/tags", new AddDeviceTagCommand(device4Id, "location=warehouse-austin"));

        // Act
        var query = "(environment=production OR environment=staging) AND location=warehouse-*";
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={Uri.EscapeDataString(query)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDevicesByTagQueryResponse>();
        result.Should().NotBeNull();
        result!.Devices.Should().HaveCount(2);
        result.Devices.Should().Contain(d => d.Id == device1Id);
        result.Devices.Should().Contain(d => d.Id == device2Id);
        result.Devices.Should().NotContain(d => d.Id == device3Id);
        result.Devices.Should().NotContain(d => d.Id == device4Id);
    }

    [Fact]
    public async Task SearchDevicesByTagQuery_WithInvalidQuery_ReturnsBadRequest()
    {
        // Arrange
        var invalidQuery = "environment="; // Missing value

        // Act
        var response = await _client.GetAsync($"/api/devices/search?tenantId={_factory.DefaultTenantId}&query={invalidQuery}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateTestDeviceAsync(string name)
    {
        var registerCommand = new RegisterDeviceCommand(
            TenantId: _factory.DefaultTenantId,
            Name: name,
            Metadata: null);

        var response = await _client.PostAsJsonAsync("/api/devices/register", registerCommand);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        return result!.DeviceId;
    }

    #endregion
}
