using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

/// <summary>
/// X.509 certificate generator using .NET's System.Security.Cryptography.
/// </summary>
public class X509CertificateGenerator : ICertificateGenerator
{
    private const int RsaKeySize = 2048;

    public GeneratedCertificate GenerateRootCaCertificate(string subject, int validityDays)
    {
        // Generate RSA key pair
        using var rsa = RSA.Create(RsaKeySize);

        // Create certificate request
        var certRequest = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add basic constraints (CA certificate)
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Add key usage (certificate signing, CRL signing)
        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));

        // Add subject key identifier
        certRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(certRequest.PublicKey, critical: false));

        // Self-sign the certificate
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1); // 1 day grace period
        var notAfter = DateTimeOffset.UtcNow.AddDays(validityDays);

        using var certificate = certRequest.CreateSelfSigned(notBefore, notAfter);

        // Export certificate and private key to PEM
        var certificatePem = ExportCertificateToPem(certificate);
        var privateKeyPem = ExportPrivateKeyToPem(rsa);

        return new GeneratedCertificate(certificatePem, privateKeyPem);
    }

    public GeneratedCertificate GenerateDeviceCertificate(string subject, string serialNumber, int validityDays)
    {
        // Generate RSA key pair for device
        using var rsa = RSA.Create(RsaKeySize);

        // Create certificate request
        var certRequest = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add basic constraints (not a CA)
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Add key usage (digital signature, key encipherment for TLS client auth)
        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Add extended key usage (TLS client authentication)
        certRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // clientAuth
                critical: true));

        // Add subject key identifier
        certRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(certRequest.PublicKey, critical: false));

        // Export certificate request (to be signed by CA) and private key
        // Note: We create a temporary self-signed cert just to get the PEM format
        // The actual signing will happen in SignCertificate method
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(validityDays);

        using var tempCert = certRequest.CreateSelfSigned(notBefore, notAfter);

        var certificatePem = ExportCertificateToPem(tempCert);
        var privateKeyPem = ExportPrivateKeyToPem(rsa);

        return new GeneratedCertificate(certificatePem, privateKeyPem);
    }

    public string SignCertificate(string certificateRequest, string caPrivateKeyPem, string caCertificatePem)
    {
        // Load CA certificate and private key
        using var caCert = X509Certificate2.CreateFromPem(caCertificatePem, caPrivateKeyPem);
        using var caKey = RSA.Create();
        caKey.ImportFromPem(caPrivateKeyPem);

        // Load device certificate request
        using var deviceCert = X509Certificate2.CreateFromPem(certificateRequest);

        // Create a new certificate request from the device cert's public key
        var certRequest = new CertificateRequest(
            deviceCert.SubjectName,
            deviceCert.GetRSAPublicKey()!,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Copy extensions from the device certificate
        foreach (var extension in deviceCert.Extensions)
        {
            certRequest.CertificateExtensions.Add(extension);
        }

        // Add authority key identifier (links to CA)
        var subjectKeyIdentifier = caCert.Extensions
            .OfType<X509SubjectKeyIdentifierExtension>()
            .FirstOrDefault();

        if (subjectKeyIdentifier != null)
        {
            certRequest.CertificateExtensions.Add(
                new X509Extension(
                    new AsnEncodedData(
                        "2.5.29.35", // authorityKeyIdentifier OID
                        subjectKeyIdentifier.RawData),
                    critical: false));
        }

        // Generate serial number (parse from hex string)
        var serialNumberBytes = Convert.FromHexString(deviceCert.SerialNumber);

        // Sign with CA private key
        using var signedCert = certRequest.Create(
            caCert,
            deviceCert.NotBefore,
            deviceCert.NotAfter,
            serialNumberBytes);

        return ExportCertificateToPem(signedCert);
    }

    public string CalculateFingerprint(string certificatePem)
    {
        using var cert = X509Certificate2.CreateFromPem(certificatePem);
        using var sha256 = SHA256.Create();

        var hash = sha256.ComputeHash(cert.RawData);
        return Convert.ToHexString(hash);
    }

    private static string ExportCertificateToPem(X509Certificate2 certificate)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    private static string ExportPrivateKeyToPem(RSA rsa)
    {
        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");
        return sb.ToString();
    }
}
