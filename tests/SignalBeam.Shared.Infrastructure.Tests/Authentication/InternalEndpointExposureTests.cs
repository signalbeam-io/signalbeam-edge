using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Authentication;

namespace SignalBeam.Shared.Infrastructure.Tests.Authentication;

// /api/internal endpoints are for service-to-service calls only. These tests read the real
// deployment config and fail if a public route or ingress would reach them.
public class InternalEndpointExposureTests
{
    private const string InternalUrl = InternalServiceAuthentication.PathPrefix + "/devices/3f0b4c2e-8f8e-4c5e-9a55-7b6f4f0a1c11/tenant";

    // Only these apps may have public ingress (Container Apps) or a root ("/") ingress path (Helm).
    private static readonly string[] PublicApps = ["apigateway", "api-gateway", "zitadel", "web"];

    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("src/SignalBeam.ApiGateway/appsettings.json")]
    [InlineData("src/SignalBeam.ApiGateway/appsettings.Development.json")]
    public void GatewayRoutes_DoNotMatchInternalPaths(string file)
    {
        // Arrange
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, file)));
        var routes = config.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject().ToList();

        // Act
        var matching = routes
            .Where(r => RouteMatches(r.Value.GetProperty("Match").GetProperty("Path").GetString()!, InternalUrl))
            .Select(r => r.Name)
            .ToList();

        // Assert
        routes.Should().NotBeEmpty();
        matching.Should().BeEmpty("the gateway is public and must not proxy {0}", InternalServiceAuthentication.PathPrefix);
    }

    [Fact]
    public void HelmIngressPaths_DoNotCoverInternalPaths()
    {
        // Arrange
        var valueFiles = Directory.GetFiles(Path.Combine(RepoRoot, "deploy", "charts"), "values*.yaml", SearchOption.AllDirectories);

        // Act
        var exposing = (
            from file in valueFiles
            let chart = Path.GetFileName(Path.GetDirectoryName(file))!
            where !PublicApps.Contains(chart)
            from match in Regex.Matches(File.ReadAllText(file), @"^\s*-\s*path:\s*(\S+)", RegexOptions.Multiline)
            let path = match.Groups[1].Value.Trim('"', '\'')
            where InternalUrl.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal) || path == "/"
            select $"{Path.GetRelativePath(RepoRoot, file)}: {path}").ToList();

        // Assert
        valueFiles.Should().NotBeEmpty();
        exposing.Should().BeEmpty();
    }

    [Fact]
    public void ContainerApps_OnlyGatewayAndZitadelHavePublicIngress()
    {
        // Arrange
        var units = Directory.GetFiles(Path.Combine(RepoRoot, "infra", "terragrunt"), "terragrunt.hcl", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".terragrunt-cache", StringComparison.Ordinal));

        // Act
        var external = units
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^\s*ingress_external\s*=\s*true", RegexOptions.Multiline))
            .Select(f => Path.GetFileName(Path.GetDirectoryName(f))!)
            .Where(app => !PublicApps.Contains(app))
            .ToList();

        // Assert
        external.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/devices/{**catch-all}", "/api/devices/x/heartbeat", true)]
    [InlineData("/api/devices/{deviceId}/desired-state", "/api/devices/x/desired-state", true)]
    [InlineData("/api/devices/{**catch-all}", InternalUrl, false)]
    [InlineData("/{**catch-all}", InternalUrl, true)]
    public void RouteMatches_FollowsYarpTemplates(string template, string path, bool expected)
    {
        RouteMatches(template, path).Should().Be(expected);
    }

    private static bool RouteMatches(string template, string path)
    {
        var pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{\\\*\\\*[^}]*}|\{\*\*[^}]*}", ".*");
        pattern = Regex.Replace(pattern, @"\\\{[^}]*}|\{[^}]*}", "[^/]+") + "$";
        return Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "SignalBeam.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (src/SignalBeam.sln) not found.");
    }
}
