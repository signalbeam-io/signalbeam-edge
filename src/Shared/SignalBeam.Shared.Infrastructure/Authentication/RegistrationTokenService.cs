using System.Security.Cryptography;

namespace SignalBeam.Shared.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating registration tokens using BCrypt.
/// </summary>
public class RegistrationTokenService : IRegistrationTokenService
{
    private const string TokenPrefix = "sbt_"; // SignalBeam Token
    private const int PrefixLength = 8; // First 8 chars after sbt_
    private const int SecretLength = 32; // Random secret part
    private const int WorkFactor = 12; // BCrypt work factor

    public (string PlainTextToken, string TokenHash, string TokenPrefix) GenerateToken()
    {
        // Generate prefix (8 chars from GUID)
        var prefix = Guid.NewGuid().ToString("N")[..PrefixLength];

        // Generate secret (32 random characters)
        var secret = GenerateSecureSecret(SecretLength);

        // Combine: sbt_{prefix}_{secret}
        var plainTextToken = $"{TokenPrefix}{prefix}_{secret}";

        // Hash the full token with BCrypt
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(plainTextToken, workFactor: WorkFactor);

        // Return token prefix for database lookup: sbt_{prefix}
        var tokenPrefixForLookup = $"{TokenPrefix}{prefix}";

        return (plainTextToken, tokenHash, tokenPrefixForLookup);
    }

    public bool ValidateToken(string plainTextToken, string storedHash)
    {
        if (string.IsNullOrEmpty(plainTextToken) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainTextToken, storedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateSecureSecret(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[length];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }
}
