namespace SignalBeam.EdgeAgent.Application.Services;

/// <summary>
/// Detects when the device API key is approaching expiry and rotates it with the cloud,
/// persisting the new key locally.
/// </summary>
public interface IKeyRotationService
{
    /// <summary>
    /// Rotates the API key if it expires within the configured threshold.
    /// Returns true if a rotation occurred, false if no rotation was needed.
    /// </summary>
    Task<bool> CheckAndRotateAsync(CancellationToken cancellationToken = default);
}
