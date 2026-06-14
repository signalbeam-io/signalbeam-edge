using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Security;

/// <summary>
/// Generates an RSA key pair and a PKCS#10 CSR using System.Security.Cryptography.
/// The private key is returned to the caller to be stored locally and never transmitted.
/// </summary>
public sealed class CsrGenerator : ICsrGenerator
{
    private const int RsaKeySize = 2048;

    public (string CsrPem, string PrivateKeyPem) GenerateCsr(string subject)
    {
        using var rsa = RSA.Create(RsaKeySize);

        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrPem = request.CreateSigningRequestPem();
        var privateKeyPem = ExportPrivateKeyToPem(rsa);

        return (csrPem, privateKeyPem);
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
