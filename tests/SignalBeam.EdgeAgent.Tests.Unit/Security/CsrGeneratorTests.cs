using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SignalBeam.EdgeAgent.Infrastructure.Security;

namespace SignalBeam.EdgeAgent.Tests.Unit.Security;

public class CsrGeneratorTests
{
    [Fact]
    public void GenerateCsr_ProducesLoadableCsr_WithSubjectAndMatchingKey()
    {
        var generator = new CsrGenerator();

        var (csrPem, privateKeyPem) = generator.GenerateCsr("CN=device-xyz, O=SignalBeam");

        // The CSR is a valid PKCS#10 request carrying the expected subject.
        var request = CertificateRequest.LoadSigningRequestPem(csrPem, HashAlgorithmName.SHA256);
        request.SubjectName.Name.Should().Contain("device-xyz");

        // The returned private key matches the CSR's public key.
        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(privateKeyPem);
        request.PublicKey.GetRSAPublicKey()!.ExportSubjectPublicKeyInfo()
            .Should().Equal(privateKey.ExportSubjectPublicKeyInfo());
    }

    [Fact]
    public void GenerateCsr_ProducesDistinctKeysPerCall()
    {
        var generator = new CsrGenerator();

        var (_, key1) = generator.GenerateCsr("CN=a");
        var (_, key2) = generator.GenerateCsr("CN=a");

        key1.Should().NotBe(key2);
    }
}
