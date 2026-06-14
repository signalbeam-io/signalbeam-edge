using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Storage;

/// <summary>
/// Stores mTLS certificate material as PEM files in a configured directory. The private key is
/// written with owner-only permissions (0600) on Unix. Writes go to temp files first and are then
/// moved into place so a partial write never leaves a half-written key.
/// </summary>
public sealed class FileCertificateStore : ICertificateStore
{
    private const string CertFileName = "device.crt";
    private const string KeyFileName = "device.key";
    private const string CaFileName = "ca.crt";

    private readonly string _directory;

    public FileCertificateStore(string directory)
    {
        _directory = directory;
    }

    public async Task<StoredCertificatePaths> SaveAsync(
        string certificatePem,
        string privateKeyPem,
        string caCertificatePem,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);

        var certPath = Path.Combine(_directory, CertFileName);
        var keyPath = Path.Combine(_directory, KeyFileName);
        var caPath = Path.Combine(_directory, CaFileName);

        await WriteAtomicAsync(keyPath, privateKeyPem, ownerOnly: true, cancellationToken);
        await WriteAtomicAsync(certPath, certificatePem, ownerOnly: false, cancellationToken);
        await WriteAtomicAsync(caPath, caCertificatePem, ownerOnly: false, cancellationToken);

        return new StoredCertificatePaths(certPath, keyPath, caPath);
    }

    private static async Task WriteAtomicAsync(
        string path,
        string content,
        bool ownerOnly,
        CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";

        // Create the file with restrictive permissions BEFORE writing sensitive content (Unix).
        if (ownerOnly && !OperatingSystem.IsWindows())
        {
            await File.WriteAllTextAsync(tempPath, string.Empty, cancellationToken);
            File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}
