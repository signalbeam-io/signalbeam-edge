namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating registration tokens.
/// </summary>
public interface IRegistrationTokenService
{
    /// <summary>
    /// Generates a new registration token.
    /// Format: sbt_{prefix}_{secret}
    /// where prefix is first 8 chars of a random GUID, secret is 32 random chars.
    /// </summary>
    /// <returns>
    /// Tuple containing:
    /// - PlainTextToken: The full token to give to the user (sbt_abc12345_...)
    /// - TokenHash: BCrypt hash to store in database
    /// - TokenPrefix: First part for lookup (sbt_abc12345)
    /// </returns>
    (string PlainTextToken, string TokenHash, string TokenPrefix) GenerateToken();

    /// <summary>
    /// Validates a registration token against its stored hash.
    /// </summary>
    /// <param name="plainTextToken">The token provided by the user</param>
    /// <param name="storedHash">The BCrypt hash from database</param>
    /// <returns>True if token is valid</returns>
    bool ValidateToken(string plainTextToken, string storedHash);
}
