using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SignalBeam.DeviceManager.Infrastructure.CertificateAuthority;

namespace SignalBeam.DeviceManager.Tests.Unit.CertificateAuthority;

public class X509CertificateGeneratorCsrTests
{
    private readonly X509CertificateGenerator _generator = new();

    [Fact]
    public void SignCertificateSigningRequest_ProducesClientCertChainingToCa()
    {
        // Arrange: a root CA, and a device-generated CSR (private key stays with the device).
        var ca = _generator.GenerateRootCaCertificate("CN=Test CA, O=SignalBeam, C=US", validityDays: 3650);

        using var deviceKey = RSA.Create(2048);
        var deviceRequest = new CertificateRequest(
            "CN=device-abc, O=SignalBeam", deviceKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var csrPem = deviceRequest.CreateSigningRequestPem();

        var serial = Convert.ToHexString(RandomNumberGenerator.GetBytes(19)); // positive, even-length hex

        // Act
        var signedPem = _generator.SignCertificateSigningRequest(
            csrPem, ca.PrivateKeyPem, ca.CertificatePem, serial, validityDays: 90);

        // Assert
        using var signed = X509Certificate2.CreateFromPem(signedPem);
        using var caCert = X509Certificate2.CreateFromPem(ca.CertificatePem);

        signed.Subject.Should().Contain("device-abc");
        signed.IssuerName.Name.Should().Be(caCert.SubjectName.Name);

        // The signed cert's public key must match the device's retained private key.
        signed.GetRSAPublicKey()!.ExportSubjectPublicKeyInfo()
            .Should().Equal(deviceKey.ExportSubjectPublicKeyInfo());

        // clientAuth EKU is present (CA-controlled, not from the CSR).
        var eku = signed.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        eku.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).Should().Contain("1.3.6.1.5.5.7.3.2");

        // It chains to the CA.
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);
        chain.Build(signed).Should().BeTrue("the signed device certificate should chain to the CA");
    }
}
