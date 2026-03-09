using Microsoft.Extensions.Logging;
using SignalBeam.DeviceManager.Application.Services;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// In-memory CA key store for development and staging environments.
/// WARNING: CA private key is stored in process memory and lost on restart.
/// Do not use in production — use AzureKeyVaultCaKeyStore instead.
/// </summary>
public class InMemoryCaKeyStore : ICaKeyStore
{
    private readonly ILogger<InMemoryCaKeyStore> _logger;
    private string? _certificatePem;
    private string? _privateKeyPem;

    public InMemoryCaKeyStore(ILogger<InMemoryCaKeyStore> logger)
    {
        _logger = logger;
    }

    public Task<bool> CaKeyExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_certificatePem != null && _privateKeyPem != null);
    }

    public Task StoreCaKeyAsync(
        string certificatePem,
        string privateKeyPem,
        CancellationToken cancellationToken = default)
    {
        _certificatePem = certificatePem;
        _privateKeyPem = privateKeyPem;

        _logger.LogWarning(
            "CA private key stored in memory. This is acceptable for development but NOT for production. " +
            "Use Azure Key Vault for production deployments.");

        return Task.CompletedTask;
    }

    public Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default)
    {
        if (_certificatePem == null)
            throw new InvalidOperationException("CA certificate not available. Initialize the CA first.");

        return Task.FromResult(_certificatePem);
    }

    public Task<string> GetCaPrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_privateKeyPem == null)
            throw new InvalidOperationException("CA private key not available. Initialize the CA first.");

        return Task.FromResult(_privateKeyPem);
    }
}
