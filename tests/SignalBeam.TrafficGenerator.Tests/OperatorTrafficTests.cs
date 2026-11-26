using System.Net;
using System.Text;
using SignalBeam.TrafficGenerator;

namespace SignalBeam.TrafficGenerator.Tests;

public class OperatorTrafficTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Device = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Group = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnBundle = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserBundle = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PendingRollout = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ActiveAlert = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public void ActionPicker_SameSeed_GivesSameSequence()
    {
        // Act
        var first = Sequence(seed: 7, count: 200);
        var second = Sequence(seed: 7, count: 200);
        var other = Sequence(seed: 8, count: 200);

        // Assert
        first.Should().Equal(second);
        first.Should().NotEqual(other);
    }

    [Fact]
    public void ActionPicker_CoversEveryActionAndMostlyReads()
    {
        // Act
        var actions = Sequence(seed: 1, count: 20_000, invalidRate: 0.05);

        // Assert
        actions.Distinct().Should().BeEquivalentTo(Enum.GetValues<OperatorAction>());
        actions.Count(a => a == OperatorAction.InvalidRequest).Should().BeInRange(800, 1200);
        actions.Count(a => a is OperatorAction.ListDevices or OperatorAction.GetDevice)
            .Should().BeGreaterThan(actions.Count(a => a == OperatorAction.CreateBundle) * 10);
    }

    [Theory]
    [InlineData(new[] { "1.0.0" }, "1.0.1")]
    [InlineData(new[] { "1.0.0", "1.2.3", "1.0.9" }, "1.2.4")]
    [InlineData(new string[0], "1.0.1")]
    public void NextVersion_BumpsTheHighestPatch(string[] versions, string expected)
    {
        OperatorState.NextVersion(versions).Should().Be(expected);
    }

    [Fact]
    public async Task WarmUp_ReadsTheListsTheUiShows()
    {
        // Arrange
        var (session, gateway) = Session();

        // Act
        await session.WarmUpAsync(CancellationToken.None);

        // Assert
        session.State.DeviceIds.Should().Equal(Device);
        session.State.GroupIds.Should().Equal(Group);
        session.State.Bundles.Keys.Should().BeEquivalentTo([OwnBundle, UserBundle]);
        session.State.Rollouts.Should().ContainKey(PendingRollout).WhoseValue.Should().Be("Pending");
        session.State.Alerts.Should().ContainKey(ActiveAlert);
        gateway.Requests.Should().OnlyContain(r => r.ApiKey == "test-key");
    }

    [Fact]
    public async Task StartRollout_StartsAPendingRollout()
    {
        // Arrange
        var (session, gateway) = Session();
        await session.WarmUpAsync(CancellationToken.None);

        // Act
        var status = await session.RunAsync(OperatorAction.StartRollout, CancellationToken.None);

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        gateway.Requests.Should().Contain(r => r.Method == "POST" && r.Path == $"/api/phased-rollouts/{PendingRollout}/start");
        session.State.Rollouts[PendingRollout].Should().Be("InProgress");
    }

    [Fact]
    public async Task PauseRollout_WithNothingInProgress_FallsBackToTheList()
    {
        // Arrange
        var (session, gateway) = Session();
        await session.WarmUpAsync(CancellationToken.None);
        gateway.Requests.Clear();

        // Act
        await session.RunAsync(OperatorAction.PauseRollout, CancellationToken.None);

        // Assert
        gateway.Requests.Should().ContainSingle().Which.Path.Should().Be("/api/phased-rollouts");
    }

    [Fact]
    public async Task CreateBundleVersion_OnlyTouchesBundlesTheGeneratorCreated()
    {
        // Arrange
        var (session, gateway) = Session();
        await session.WarmUpAsync(CancellationToken.None);

        // Act
        for (var i = 0; i < 5; i++)
        {
            await session.RunAsync(OperatorAction.CreateBundleVersion, CancellationToken.None);
        }

        // Assert
        gateway.Requests.Where(r => r.Path.EndsWith("/versions", StringComparison.Ordinal))
            .Should().HaveCount(5).And.OnlyContain(r => r.Path == $"/api/bundles/{OwnBundle}/versions");
        session.State.Bundles[OwnBundle].Versions.Should().EndWith("1.0.5");
    }

    [Fact]
    public async Task AcknowledgeAlert_SendsTheTenantAndMovesTheAlertOn()
    {
        // Arrange
        var (session, gateway) = Session();
        await session.WarmUpAsync(CancellationToken.None);

        // Act
        await session.RunAsync(OperatorAction.AcknowledgeAlert, CancellationToken.None);

        // Assert
        gateway.Requests.Should().Contain(r =>
            r.Path == $"/api/alerts/{ActiveAlert}/acknowledge" && r.Query.Contains($"tenantId={Tenant}"));
        session.State.Alerts[ActiveAlert].Should().Be("Acknowledged");
    }

    [Fact]
    public async Task AddThenRemoveTag_UsesTheUiRoutes()
    {
        // Arrange
        var (session, gateway) = Session();
        await session.WarmUpAsync(CancellationToken.None);

        // Act
        await session.RunAsync(OperatorAction.AddDeviceTag, CancellationToken.None);
        await session.RunAsync(OperatorAction.RemoveDeviceTag, CancellationToken.None);

        // Assert
        gateway.Requests.Should().Contain(r => r.Method == "POST" && r.Path == $"/api/devices/{Device}/tags");
        gateway.Requests.Should().Contain(r => r.Method == "DELETE" && r.Path == $"/api/tags/{Device}/tags/operator%3Dchecked");
        session.State.TaggedDevices.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidRequests_ProduceClientErrors()
    {
        // Arrange
        var (session, _) = Session();

        // Act
        var statuses = new List<HttpStatusCode?>();
        for (var i = 0; i < 20; i++)
        {
            statuses.Add(await session.RunAsync(OperatorAction.InvalidRequest, CancellationToken.None));
        }

        // Assert
        statuses.Should().OnlyContain(s => s.HasValue && (int)s.Value >= 400 && (int)s.Value < 500);
    }

    [Fact]
    public async Task RunAsync_RunsTheRequestedNumberOfActions()
    {
        // Arrange
        var (session, gateway) = Session();

        // Act
        await Program.RunAsync(session, TimeSpan.FromMilliseconds(1), iterations: 10, CancellationToken.None);

        // Assert: 5 warm-up lists, then at least one request per action.
        gateway.Requests.Count.Should().BeGreaterThanOrEqualTo(15);
    }

    private static List<OperatorAction> Sequence(int seed, int count, double invalidRate = 0.05)
    {
        var picker = new ActionPicker(seed, invalidRate);
        return Enumerable.Range(0, count).Select(_ => picker.Next()).ToList();
    }

    private static (OperatorSession, FakeGateway) Session()
    {
        var gateway = new FakeGateway();
        var client = new HttpClient(gateway) { BaseAddress = new Uri("http://gateway") };
        client.DefaultRequestHeaders.Add("X-API-Key", "test-key");
        return (new OperatorSession(client, new OperatorSettings(Tenant, 0), new ActionPicker(3, 0)), gateway);
    }

    private sealed record Request(string Method, string Path, string Query, string? ApiKey);

    // Answers the list calls with one of everything, 404s unknown IDs and 400s an empty bundle name.
    private sealed class FakeGateway : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Request(request.Method.Method, request.RequestUri.AbsolutePath,
                request.RequestUri.Query, request.Headers.TryGetValues("X-API-Key", out var keys) ? keys.Single() : null));

            return (request.Method.Method, path) switch
            {
                ("GET", "/api/devices") => Json($"{{\"devices\":[{{\"id\":\"{Device}\"}}]}}"),
                ("GET", "/api/groups") => Json($"{{\"groups\":[{{\"id\":\"{Group}\"}}]}}"),
                ("GET", "/api/bundles") => Json($$"""
                    {"bundles":[
                      {"bundleId":"{{OwnBundle}}","name":"op-app-1234","versions":[{"version":"1.0.0"}]},
                      {"bundleId":"{{UserBundle}}","name":"customer-app","versions":[{"version":"2.0.0"}]}]}
                    """),
                ("GET", "/api/phased-rollouts") => Json($"{{\"rollouts\":[{{\"rolloutId\":\"{PendingRollout}\",\"status\":\"Pending\"}}]}}"),
                ("GET", "/api/alerts") => Json($"{{\"alerts\":[{{\"id\":\"{ActiveAlert}\",\"status\":0}}]}}"),
                ("POST", "/api/bundles") when body.Contains("\"name\":\"\"", StringComparison.Ordinal) => Status(HttpStatusCode.BadRequest),
                _ when path.Contains(Device.ToString(), StringComparison.Ordinal)
                       || path.Contains(OwnBundle.ToString(), StringComparison.Ordinal)
                       || path.Contains(PendingRollout.ToString(), StringComparison.Ordinal)
                       || path.Contains(ActiveAlert.ToString(), StringComparison.Ordinal)
                       || path.StartsWith("/api/tags", StringComparison.Ordinal)
                       || path == "/api/devices/search" => Status(HttpStatusCode.OK),
                _ => Status(HttpStatusCode.NotFound)
            };
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private static HttpResponseMessage Status(HttpStatusCode status) => new(status);
    }
}
