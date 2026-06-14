namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Generates a key pair and a PKCS#10 certificate signing request (CSR) on the device,
/// so the private key never leaves the device when provisioning an mTLS certificate.
/// </summary>
public interface ICsrGenerator
{
    /// <summary>
    /// Generates a fresh RSA key pair and a CSR for the given subject.
    /// </summary>
    /// <returns>The CSR and the matching private key, both PEM-encoded.</returns>
    (string CsrPem, string PrivateKeyPem) GenerateCsr(string subject);
}
