using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Application.Services;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// Azure Key Vault-backed CA key store for production environments.
/// Stores the CA certificate and private key as Key Vault secrets.
/// </summary>
public class AzureKeyVaultCaKeyStore : ICaKeyStore
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultCaKeyStore> _logger;
    private readonly AzureKeyVaultOptions _options;

    // Cache to avoid repeated Key Vault calls
    private string? _cachedCertificatePem;
    private string? _cachedPrivateKeyPem;

    public AzureKeyVaultCaKeyStore(
        ILogger<AzureKeyVaultCaKeyStore> logger,
        IOptions<AzureKeyVaultOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var credential = _options.UseManagedIdentity
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = false
            });

        _secretClient = new SecretClient(new Uri(_options.VaultUri), credential);
    }

    public async Task<bool> CaKeyExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(
                _options.CaCertSecretName,
                cancellationToken: cancellationToken);

            return response?.Value != null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task StoreCaKeyAsync(
        string certificatePem,
        string privateKeyPem,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing CA certificate and private key in Azure Key Vault");

        // Store CA certificate
        await _secretClient.SetSecretAsync(
            new KeyVaultSecret(_options.CaCertSecretName, certificatePem),
            cancellationToken);

        // Store CA private key
        await _secretClient.SetSecretAsync(
            new KeyVaultSecret(_options.CaKeySecretName, privateKeyPem),
            cancellationToken);

        _cachedCertificatePem = certificatePem;
        _cachedPrivateKeyPem = privateKeyPem;

        _logger.LogInformation("CA key material stored in Azure Key Vault successfully");
    }

    public async Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCertificatePem != null)
            return _cachedCertificatePem;

        var response = await _secretClient.GetSecretAsync(
            _options.CaCertSecretName,
            cancellationToken: cancellationToken);

        _cachedCertificatePem = response.Value.Value
            ?? throw new InvalidOperationException("CA certificate secret is empty in Key Vault.");

        return _cachedCertificatePem;
    }

    public async Task<string> GetCaPrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPrivateKeyPem != null)
            return _cachedPrivateKeyPem;

        var response = await _secretClient.GetSecretAsync(
            _options.CaKeySecretName,
            cancellationToken: cancellationToken);

        _cachedPrivateKeyPem = response.Value.Value
            ?? throw new InvalidOperationException("CA private key secret is empty in Key Vault.");

        return _cachedPrivateKeyPem;
    }
}

/// <summary>
/// Configuration options for Azure Key Vault CA key store.
/// </summary>
public class AzureKeyVaultOptions
{
    public const string SectionName = "AzureKeyVault";

    /// <summary>
    /// Azure Key Vault URI (e.g., "https://signalbeam-dev-kv.vault.azure.net/").
    /// </summary>
    public string VaultUri { get; set; } = string.Empty;

    /// <summary>
    /// Use Managed Identity for authentication. Default: true.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Secret name for the CA private key. Default: "signalbeam-ca-private-key".
    /// </summary>
    public string CaKeySecretName { get; set; } = "signalbeam-ca-private-key";

    /// <summary>
    /// Secret name for the CA certificate. Default: "signalbeam-ca-certificate".
    /// </summary>
    public string CaCertSecretName { get; set; } = "signalbeam-ca-certificate";
}
