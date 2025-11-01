namespace AIReviewer.Utils;

/// <summary>
/// Utility class for logging-related operations including cryptographic hashing.
/// </summary>
public static class Logging
{
    /// <summary>
    /// Computes a SHA-256 hash of the input string and returns it as a hexadecimal string.
    /// Used for generating fingerprints to track issues across PR iterations.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    public static string HashSha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
