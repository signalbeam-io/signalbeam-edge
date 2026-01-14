using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var zitadelUrl = Environment.GetEnvironmentVariable("ZITADEL_URL") ?? "http://localhost:9080";
var adminUser = Environment.GetEnvironmentVariable("ZITADEL_ADMIN_USER") ?? "admin";
var adminPassword = Environment.GetEnvironmentVariable("ZITADEL_ADMIN_PASSWORD") ?? "Password1!";
var outputPath = Environment.GetEnvironmentVariable("CONFIG_OUTPUT_PATH") ?? "/app/config/zitadel-config.json";

Console.WriteLine("🚀 Starting Zitadel auto-configuration...");
Console.WriteLine($"Zitadel URL: {zitadelUrl}");

var httpClient = new HttpClient { BaseAddress = new Uri(zitadelUrl) };

// Wait for Zitadel to be ready
Console.WriteLine("⏳ Waiting for Zitadel to be healthy...");
var maxAttempts = 60;
var attempt = 0;
while (attempt < maxAttempts)
{
    try
    {
        var response = await httpClient.GetAsync("/debug/ready");
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Zitadel is ready!");
            break;
        }
    }
    catch
    {
        // Ignore connection errors
    }

    attempt++;
    Console.WriteLine($"   Attempt {attempt}/{maxAttempts} - Zitadel not ready yet...");
    await Task.Delay(2000);
}

if (attempt == maxAttempts)
{
    Console.WriteLine("❌ Zitadel failed to become ready");
    Environment.Exit(1);
}

await Task.Delay(3000); // Give it a few more seconds

Console.WriteLine("🔑 Authenticating as admin...");

// Step 1: Get access token using password grant
var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "password",
    ["scope"] = "openid profile email urn:zitadel:iam:org:project:id:zitadel:aud",
    ["username"] = adminUser,
    ["password"] = adminPassword,
    ["client_id"] = "zitadel" // Built-in console client
});

var tokenResponse = await httpClient.PostAsync("/oauth/v2/token", tokenRequest);
if (!tokenResponse.IsSuccessStatusCode)
{
    var error = await tokenResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"❌ Failed to authenticate: {error}");
    Console.WriteLine("\n💡 Make sure Zitadel admin user is configured correctly");
    Console.WriteLine($"   Username: {adminUser}");
    Console.WriteLine("   Check Zitadel environment variables in AppHost");
    Environment.Exit(1);
}

var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
var accessToken = tokenData!.AccessToken;
Console.WriteLine("✅ Got access token!");

httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

// Step 2: Check if project already exists
Console.WriteLine("🔍 Checking for existing SignalBeam project...");
var projectsResponse = await httpClient.PostAsync("/management/v1/projects/_search",
    JsonContent.Create(new { }));
var projectsData = await projectsResponse.Content.ReadFromJsonAsync<ProjectsSearchResponse>();

var existingProject = projectsData?.Result?.FirstOrDefault(p => p.Name == "SignalBeam Edge");
string projectId;

if (existingProject != null)
{
    projectId = existingProject.Id;
    Console.WriteLine($"✅ Found existing project: {projectId}");
}
else
{
    // Step 3: Create project
    Console.WriteLine("🏗️  Creating SignalBeam project...");
    var projectRequest = new { name = "SignalBeam Edge" };
    var projectResponse = await httpClient.PostAsync("/management/v1/projects",
        JsonContent.Create(projectRequest));

    if (!projectResponse.IsSuccessStatusCode)
    {
        var error = await projectResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"❌ Failed to create project: {error}");
        Environment.Exit(1);
    }

    var projectData = await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>();
    projectId = projectData!.Id;
    Console.WriteLine($"✅ Project created: {projectId}");
}

// Step 4: Check if application already exists
Console.WriteLine("🔍 Checking for existing application...");
var appsResponse = await httpClient.PostAsync($"/management/v1/projects/{projectId}/apps/_search",
    JsonContent.Create(new { }));
var appsData = await appsResponse.Content.ReadFromJsonAsync<AppsSearchResponse>();

var existingApp = appsData?.Result?.FirstOrDefault(a => a.Name == "SignalBeam Web");
string clientId;

if (existingApp != null)
{
    clientId = existingApp.Id;
    Console.WriteLine($"✅ Found existing application: {clientId}");
}
else
{
    // Step 5: Create OIDC application
    Console.WriteLine("📱 Creating web application...");
    var appRequest = new
    {
        name = "SignalBeam Web",
        redirectUris = new[] { "http://localhost:5173/callback", "http://localhost:5173/silent-renew" },
        postLogoutRedirectUris = new[] { "http://localhost:5173" },
        responseTypes = new[] { "OIDC_RESPONSE_TYPE_CODE" },
        grantTypes = new[] { "OIDC_GRANT_TYPE_AUTHORIZATION_CODE", "OIDC_GRANT_TYPE_REFRESH_TOKEN" },
        appType = "OIDC_APP_TYPE_USER_AGENT",
        authMethodType = "OIDC_AUTH_METHOD_TYPE_NONE",
        version = "OIDC_VERSION_1_0",
        devMode = true,
        accessTokenType = "OIDC_TOKEN_TYPE_JWT",
        idTokenRoleAssertion = true,
        idTokenUserinfoAssertion = true,
        clockSkew = "0s"
    };

    var appResponse = await httpClient.PostAsync($"/management/v1/projects/{projectId}/apps/oidc",
        JsonContent.Create(appRequest));

    if (!appResponse.IsSuccessStatusCode)
    {
        var error = await appResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"❌ Failed to create application: {error}");
        Environment.Exit(1);
    }

    var appData = await appResponse.Content.ReadFromJsonAsync<AppResponse>();
    clientId = appData!.ClientId;
    Console.WriteLine($"✅ Application created: {clientId}");
}

// Step 6: Save configuration
Console.WriteLine($"💾 Saving configuration to {outputPath}...");

var config = new
{
    zitadel = new
    {
        authority = "http://localhost:8080",
        clientId = clientId,
        projectId = projectId,
        redirectUri = "http://localhost:5173/callback",
        postLogoutRedirectUri = "http://localhost:5173",
        scope = "openid profile email"
    },
    backend = new
    {
        authority = "http://localhost:8080",
        audience = clientId,
        requireHttpsMetadata = false
    }
};

var directory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
{
    Directory.CreateDirectory(directory);
}

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(config, jsonOptions));

Console.WriteLine("✅ Configuration saved!");
Console.WriteLine();
Console.WriteLine("📋 Zitadel Configuration:");
Console.WriteLine($"  Project ID:  {projectId}");
Console.WriteLine($"  Client ID:   {clientId}");
Console.WriteLine($"  Authority:   http://localhost:8080");
Console.WriteLine();
Console.WriteLine("🎉 Zitadel is fully configured and ready to use!");

// Record types for JSON deserialization
record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType
);

record ProjectResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name
);

record ProjectsSearchResponse(
    [property: JsonPropertyName("result")] List<ProjectResponse> Result
);

record AppResponse(
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientSecret")] string? ClientSecret
);

record AppSearchResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name
);

record AppsSearchResponse(
    [property: JsonPropertyName("result")] List<AppSearchResult> Result
);
