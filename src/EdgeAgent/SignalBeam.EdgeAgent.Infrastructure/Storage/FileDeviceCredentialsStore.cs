using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Models;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Storage;

/// <summary>
/// File-based implementation of device credentials storage.
/// Stores credentials in a JSON file on the local filesystem.
/// </summary>
public class FileDeviceCredentialsStore : IDeviceCredentialsStore
{
    private readonly string _credentialsFilePath;
    private readonly ILogger<FileDeviceCredentialsStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileDeviceCredentialsStore(
        IConfiguration configuration,
        ILogger<FileDeviceCredentialsStore> logger)
    {
        _logger = logger;

        // Get credentials file path from configuration or use default
        var baseDir = configuration["Agent:CredentialsDirectory"] ?? "/var/lib/signalbeam-agent";
        var fileName = "device-credentials.json";
        _credentialsFilePath = Path.Combine(baseDir, fileName);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_credentialsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created credentials directory: {Directory}", directory);
        }

        _logger.LogInformation("Using credentials file path: {FilePath}", _credentialsFilePath);
    }

    public async Task SaveCredentialsAsync(DeviceCredentials credentials, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_credentialsFilePath, json, cancellationToken);

            // Set file permissions to 600 (owner read/write only) on Unix systems
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            _logger.LogInformation(
                "Saved device credentials for device {DeviceId} with status {Status}",
                credentials.DeviceId,
                credentials.RegistrationStatus);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DeviceCredentials?> LoadCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_credentialsFilePath))
            {
                _logger.LogDebug("Credentials file does not exist: {FilePath}", _credentialsFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_credentialsFilePath, cancellationToken);
            var credentials = JsonSerializer.Deserialize<DeviceCredentials>(json);

            if (credentials != null)
            {
                _logger.LogInformation(
                    "Loaded device credentials for device {DeviceId} with status {Status}",
                    credentials.DeviceId,
                    credentials.RegistrationStatus);
            }

            return credentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device credentials from {FilePath}", _credentialsFilePath);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> CredentialsExistAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async for consistency
        return File.Exists(_credentialsFilePath);
    }

    public async Task DeleteCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_credentialsFilePath))
            {
                File.Delete(_credentialsFilePath);
                _logger.LogInformation("Deleted device credentials file: {FilePath}", _credentialsFilePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateApiKeyAsync(string apiKey, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var credentials = await LoadCredentialsAsync(cancellationToken);
            if (credentials == null)
            {
                throw new InvalidOperationException("Cannot update API key: credentials not found");
            }

            credentials.ApiKey = apiKey;
            credentials.ApiKeyExpiresAt = expiresAt;

            await SaveCredentialsAsync(credentials, cancellationToken);

            _logger.LogInformation(
                "Updated API key for device {DeviceId}, expires at {ExpiresAt}",
                credentials.DeviceId,
                expiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "never");
        }
        finally
        {
            _lock.Release();
        }
    }
}
