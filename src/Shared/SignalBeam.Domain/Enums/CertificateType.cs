namespace SignalBeam.Domain.Enums;

/// <summary>
/// Represents the type of X.509 certificate.
/// </summary>
public enum CertificateType
{
    /// <summary>
    /// Root Certificate Authority certificate.
    /// Self-signed certificate at the top of the certificate chain.
    /// </summary>
    RootCA = 1,

    /// <summary>
    /// Intermediate Certificate Authority certificate.
    /// Signed by Root CA, used to sign device certificates.
    /// </summary>
    IntermediateCA = 2,

    /// <summary>
    /// Device client certificate for mTLS authentication.
    /// Signed by CA, used by edge devices to authenticate.
    /// </summary>
    Device = 3
}
