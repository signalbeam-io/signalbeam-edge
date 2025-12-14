using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Extension methods for configuring Azure AD / Entra ID authentication.
/// </summary>
public static class AzureAdConfiguration
{
    /// <summary>
    /// Adds Azure AD JWT Bearer authentication.
    /// </summary>
    public static IServiceCollection AddAzureAdAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        return services;
    }

    /// <summary>
    /// Adds Azure Managed Identity credential provider.
    /// </summary>
    public static IServiceCollection AddManagedIdentityCredential(this IServiceCollection services)
    {
        services.AddSingleton<DefaultAzureCredential>(_ => new DefaultAzureCredential());
        return services;
    }

    /// <summary>
    /// Gets a DefaultAzureCredential for authenticating with Azure services.
    /// </summary>
    public static DefaultAzureCredential GetAzureCredential()
    {
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true
        });
    }
}

/// <summary>
/// Azure AD configuration options.
/// </summary>
public class AzureAdOptions
{
    /// <summary>
    /// Azure AD instance URL (e.g., https://login.microsoftonline.com/).
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Application (client) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Application audience (e.g., api://your-app-id).
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}
