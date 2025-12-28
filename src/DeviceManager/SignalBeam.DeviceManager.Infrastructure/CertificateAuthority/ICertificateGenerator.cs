namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// Service for generating X.509 certificates.
/// </summary>
public interface ICertificateGenerator
{
    /// <summary>
    /// Generates a root CA certificate (self-signed).
    /// </summary>
    /// <param name="subject">Certificate subject (e.g., "CN=SignalBeam Root CA, O=SignalBeam, C=US").</param>
    /// <param name="validityDays">Number of days the CA is valid.</param>
    /// <returns>Generated certificate with private key.</returns>
    GeneratedCertificate GenerateRootCaCertificate(string subject, int validityDays);

    /// <summary>
    /// Generates a device client certificate.
    /// Note: This generates the certificate request. It must be signed by the CA separately.
    /// </summary>
    /// <param name="subject">Certificate subject (e.g., "CN=device-{deviceId}, O=SignalBeam").</param>
    /// <param name="serialNumber">Unique serial number for the certificate.</param>
    /// <param name="validityDays">Number of days the certificate is valid.</param>
    /// <returns>Generated certificate (unsigned) with private key.</returns>
    GeneratedCertificate GenerateDeviceCertificate(string subject, string serialNumber, int validityDays);

    /// <summary>
    /// Signs a certificate with the CA private key.
    /// </summary>
    /// <param name="certificateRequest">The certificate request to sign.</param>
    /// <param name="caPrivateKeyPem">CA private key in PEM format.</param>
    /// <param name="caCertificatePem">CA certificate in PEM format.</param>
    /// <returns>Signed certificate in PEM format.</returns>
    string SignCertificate(string certificateRequest, string caPrivateKeyPem, string caCertificatePem);

    /// <summary>
    /// Calculates the SHA-256 fingerprint of a certificate.
    /// </summary>
    /// <param name="certificatePem">Certificate in PEM format.</param>
    /// <returns>Hexadecimal fingerprint string.</returns>
    string CalculateFingerprint(string certificatePem);
}

/// <summary>
/// Represents a generated certificate with private key.
/// </summary>
public record GeneratedCertificate(
    string CertificatePem,
    string PrivateKeyPem);
