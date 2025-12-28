namespace SignalBeam.Shared.Infrastructure.Http;

/// <summary>
/// Service to extract HTTP context information for audit logging.
/// </summary>
public interface IHttpContextInfoProvider
{
    /// <summary>
    /// Gets the client IP address from the current HTTP request.
    /// </summary>
    string? GetClientIpAddress();

    /// <summary>
    /// Gets the User-Agent header from the current HTTP request.
    /// </summary>
    string? GetUserAgent();
}
