namespace SignalBeam.DeviceManager.Application.Services;

/// <summary>
/// Abstraction for CA private key storage and signing operations.
/// Implementations can store keys in memory (dev), file system, or Azure Key Vault (production).
/// </summary>
public interface ICaKeyStore
{
    /// <summary>
    /// Checks if the CA key material already exists in the store.
    /// </summary>
    Task<bool> CaKeyExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the CA certificate and private key.
    /// Called during initial CA setup.
    /// </summary>
    Task StoreCaKeyAsync(
        string certificatePem,
        string privateKeyPem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the CA certificate in PEM format.
    /// </summary>
    Task<string> GetCaCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the CA private key in PEM format for signing operations.
    /// For Key Vault implementations, this may perform remote signing instead.
    /// </summary>
    Task<string> GetCaPrivateKeyAsync(CancellationToken cancellationToken = default);
}
