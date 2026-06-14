namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Persists the device's mTLS certificate material to local storage.
/// </summary>
public interface ICertificateStore
{
    /// <summary>
    /// Writes the certificate, private key, and CA certificate to disk (the private key with
    /// restrictive permissions) and returns the resulting file paths.
    /// </summary>
    Task<StoredCertificatePaths> SaveAsync(
        string certificatePem,
        string privateKeyPem,
        string caCertificatePem,
        CancellationToken cancellationToken = default);
}

public record StoredCertificatePaths(
    string CertificatePath,
    string PrivateKeyPath,
    string CaCertificatePath);
