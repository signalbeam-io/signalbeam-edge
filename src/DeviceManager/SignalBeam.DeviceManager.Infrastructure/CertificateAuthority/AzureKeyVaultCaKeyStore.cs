using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Application.Services;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// Azure Key Vault-backed CA key store for production environments.
/// Stores the CA certificate and private key as Key Vault secrets.
/// Private key cache has a TTL to minimize time key material is in memory.
/// </summary>
public class AzureKeyVaultCaKeyStore : ICaKeyStore
{
    private static readonly TimeSpan PrivateKeyCacheTtl = TimeSpan.FromMinutes(5);

    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultCaKeyStore> _logger;
    private readonly AzureKeyVaultOptions _options;

    // CA certificate is public and can be cached indefinitely
    private string? _cachedCertificatePem;

    // Private key cache with TTL to minimize exposure in memory
    private string? _cachedPrivateKeyPem;
    private DateTimeOffset _privateKeyCacheExpiry = DateTimeOffset.MinValue;
    private readonly object _cacheLock = new();

    public AzureKeyVaultCaKeyStore(
        ILogger<AzureKeyVaultCaKeyStore> logger,
        IOptions<AzureKeyVaultOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var credential = new DefaultAzureCredential();
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

        await _secretClient.SetSecretAsync(
            new KeyVaultSecret(_options.CaCertSecretName, certificatePem),
            cancellationToken);

        await _secretClient.SetSecretAsync(
            new KeyVaultSecret(_options.CaKeySecretName, privateKeyPem),
            cancellationToken);

        _cachedCertificatePem = certificatePem;

        lock (_cacheLock)
        {
            _cachedPrivateKeyPem = privateKeyPem;
            _privateKeyCacheExpiry = DateTimeOffset.UtcNow.Add(PrivateKeyCacheTtl);
        }

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
        lock (_cacheLock)
        {
            if (_cachedPrivateKeyPem != null && DateTimeOffset.UtcNow < _privateKeyCacheExpiry)
                return _cachedPrivateKeyPem;
        }

        var response = await _secretClient.GetSecretAsync(
            _options.CaKeySecretName,
            cancellationToken: cancellationToken);

        var privateKey = response.Value.Value
            ?? throw new InvalidOperationException("CA private key secret is empty in Key Vault.");

        lock (_cacheLock)
        {
            _cachedPrivateKeyPem = privateKey;
            _privateKeyCacheExpiry = DateTimeOffset.UtcNow.Add(PrivateKeyCacheTtl);
        }

        return privateKey;
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
