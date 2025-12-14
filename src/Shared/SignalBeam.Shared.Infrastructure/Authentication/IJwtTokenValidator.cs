using SignalBeam.Shared.Infrastructure.Results;
using System.Security.Claims;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Abstraction for validating JWT tokens.
/// </summary>
public interface IJwtTokenValidator
{
    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the claims principal if valid, or an error if invalid.</returns>
    Task<Result<ClaimsPrincipal>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts claims from a JWT token without full validation.
    /// Warning: Use only for logging/debugging. Do not use for authorization decisions.
    /// </summary>
    /// <param name="token">The JWT token to extract claims from.</param>
    /// <returns>The extracted claims, or null if the token is invalid.</returns>
    IEnumerable<Claim>? ExtractClaims(string token);
}
